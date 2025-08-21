using LzAssessor.NewVersion.Engine;
using LzAssessor.NewVersion.Models;
using LzAssessor.NewVersion.Persistence;
using LzAssessor.NewVersion.Specs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace LzAssessor.NewVersion.Triggers;

/// <summary>
/// Orchestrated triggers using Durable Functions for robust execution
/// </summary>
public static class OrchestrationTriggers
{
    /// <summary>
    /// HTTP trigger to start assessment orchestration
    /// </summary>
    [Function(nameof(StartAssessmentOrchestration))]
    public static async Task<HttpResponseData> StartAssessmentOrchestration(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "assessment/run-orchestrated")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(StartAssessmentOrchestration));
        
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = string.IsNullOrEmpty(requestBody) 
                ? new AssessmentRequest() 
                : JsonSerializer.Deserialize<AssessmentRequest>(requestBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            var runId = string.IsNullOrEmpty(request?.RunId) ? Guid.NewGuid().ToString() : request.RunId;
            
            logger.LogInformation("Starting assessment orchestration {RunId}", runId);

            // Start the orchestration
            var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(AssessmentOrchestrator), 
                (request ?? new AssessmentRequest()) with { RunId = runId });

            logger.LogInformation("Started orchestration {InstanceId} for run {RunId}", instanceId, runId);

            // Return orchestration status
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new 
            { 
                runId, 
                instanceId,
                statusQueryGetUri = $"{req.Url.Scheme}://{req.Url.Host}/runtime/webhooks/durabletask/instances/{instanceId}",
                message = "Assessment orchestration started"
            });

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start assessment orchestration");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Main assessment orchestrator
    /// </summary>
    [Function(nameof(AssessmentOrchestrator))]
    public static async Task<AssessmentRun> AssessmentOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(AssessmentOrchestrator));
        var request = context.GetInput<AssessmentRequest>()!;
        var runId = request.RunId;

        logger.LogInformation("Starting assessment orchestration for run {RunId}", runId);

        try
        {
            // Step 1: Discovery
            var snapshot = await context.CallActivityAsync<DiscoverySnapshot>(
                nameof(DiscoveryActivity), 
                request.TenantId);

            logger.LogInformation("Discovery completed for tenant {TenantId}", snapshot.TenantId);

            // Step 2: Load specification
            var spec = await context.CallActivityAsync<SpecFile>(
                nameof(LoadSpecActivity), 
                request.SpecUrl);

            logger.LogInformation("Loaded spec {SpecVersion} with {CheckCount} checks", 
                spec.SpecVersion, spec.Checks.Count);

            // Step 3: Execute checks
            var executionInput = new CheckExecutionInput(
                runId,
                snapshot.TenantId,
                request.Subscriptions?.FirstOrDefault() ?? "tenant",
                spec.Checks.ToArray(),
                snapshot);

            var results = await context.CallActivityAsync<AssessmentResult[]>(
                nameof(ExecuteChecksActivity), 
                executionInput);

            logger.LogInformation("Executed {CheckCount} checks", results.Length);

            // Step 4: Apply attestations to results
            var enhancedResults = await context.CallActivityAsync<AssessmentResult[]>(
                nameof(ApplyAttestationsActivity), 
                new AttestationInput(snapshot.TenantId, results));

            logger.LogInformation("Applied attestations to {CheckCount} checks", enhancedResults.Length);

            // Step 5: Create assessment run
            var run = new AssessmentRun(
                RunId: runId,
                TenantId: snapshot.TenantId,
                SpecVersion: spec.SpecVersion,
                Category: spec.Category,
                Results: enhancedResults,
                Snapshot: snapshot,
                StartedAt: context.CurrentUtcDateTime.AddMinutes(-5), // Approximate start time
                CompletedAt: context.CurrentUtcDateTime,
                TotalChecks: enhancedResults.Length,
                CompliantChecks: enhancedResults.Count(r => r.Status == AssessmentStatus.Compliant),
                NonCompliantChecks: enhancedResults.Count(r => r.Status == AssessmentStatus.NonCompliant),
                ManualChecks: enhancedResults.Count(r => r.Status == AssessmentStatus.ManualRequired));

            // Step 6: Persist results
            await context.CallActivityAsync(nameof(PersistResultsActivity), run);

            logger.LogInformation("Assessment run {RunId} completed with {CompliantCount}/{TotalCount} compliant checks", 
                runId, run.CompliantChecks, run.TotalChecks);

            return run;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Assessment orchestration failed for run {RunId}", runId);
            throw;
        }
    }

    /// <summary>
    /// Activity function for discovery
    /// </summary>
    [Function(nameof(DiscoveryActivity))]
    public static async Task<DiscoverySnapshot> DiscoveryActivity(
        [ActivityTrigger] string? tenantId,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(DiscoveryActivity));
        var discoveryService = context.InstanceServices.GetRequiredService<IDiscoveryService>();

        logger.LogInformation("Starting discovery for tenant {TenantId}", tenantId ?? "auto-discover");

        var snapshot = !string.IsNullOrEmpty(tenantId) 
            ? await discoveryService.DiscoverAsync(tenantId)
            : await discoveryService.DiscoverAsync();

        logger.LogInformation("Discovery completed for tenant {TenantId} with {SubscriptionCount} subscriptions", 
            snapshot.TenantId, snapshot.Subscriptions.Length);

        return snapshot;
    }

    /// <summary>
    /// Activity function for loading specifications
    /// </summary>
    [Function(nameof(LoadSpecActivity))]
    public static async Task<SpecFile> LoadSpecActivity(
        [ActivityTrigger] string? specUrl,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(LoadSpecActivity));
        var specLoader = context.InstanceServices.GetRequiredService<ISpecLoader>();

        logger.LogInformation("Loading specification from {SpecUrl}", specUrl ?? "default");

        var spec = await specLoader.LoadAsync(specUrl);

        logger.LogInformation("Loaded spec {SpecVersion} with {CheckCount} checks", 
            spec.SpecVersion, spec.Checks.Count);

        return spec;
    }

    /// <summary>
    /// Activity function for executing checks
    /// </summary>
    [Function(nameof(ExecuteChecksActivity))]
    public static async Task<AssessmentResult[]> ExecuteChecksActivity(
        [ActivityTrigger] CheckExecutionInput input,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(ExecuteChecksActivity));
        var router = context.InstanceServices.GetRequiredService<ExecutionRouter>();

        logger.LogInformation("Executing {CheckCount} checks for run {RunId}", 
            input.Checks.Length, input.RunId);

        var results = await router.ExecuteChecksAsync(
            input.RunId,
            input.TenantId,
            input.Scope,
            input.Checks,
            input.Snapshot,
            maxConcurrency: 3);

        logger.LogInformation("Completed execution of {CheckCount} checks", results.Length);

        return results;
    }

    /// <summary>
    /// Activity function for applying attestations
    /// </summary>
    [Function(nameof(ApplyAttestationsActivity))]
    public static async Task<AssessmentResult[]> ApplyAttestationsActivity(
        [ActivityTrigger] AttestationInput input,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(ApplyAttestationsActivity));
        var attestationService = context.InstanceServices.GetRequiredService<IAttestationEvaluationService>();

        logger.LogInformation("Applying attestations for tenant {TenantId} to {CheckCount} results", 
            input.TenantId, input.Results.Length);

        var enhancedResults = await attestationService.ApplyAttestationsAsync(
            input.TenantId, input.Results);

        var attestedCount = enhancedResults.Count(r => r.Metadata.ContainsKey("attestation"));
        logger.LogInformation("Applied {AttestedCount} attestations to results", attestedCount);

        return enhancedResults;
    }

    /// <summary>
    /// Activity function for persisting results
    /// </summary>
    [Function(nameof(PersistResultsActivity))]
    public static async Task PersistResultsActivity(
        [ActivityTrigger] AssessmentRun run,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(PersistResultsActivity));
        var persistence = context.InstanceServices.GetRequiredService<IAssessmentPersistence>();

        logger.LogInformation("Persisting results for run {RunId}", run.RunId);

        await persistence.SaveAssessmentRunAsync(run);

        logger.LogInformation("Results persisted for run {RunId}", run.RunId);
    }
}

/// <summary>
/// Input for check execution activity
/// </summary>
public record CheckExecutionInput(
    string RunId,
    string TenantId,
    string Scope,
    SpecCheck[] Checks,
    DiscoverySnapshot Snapshot);

/// <summary>
/// Input for attestation application activity
/// </summary>
public record AttestationInput(
    string TenantId,
    AssessmentResult[] Results);