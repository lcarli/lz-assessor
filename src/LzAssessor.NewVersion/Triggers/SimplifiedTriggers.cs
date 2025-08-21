using LzAssessor.NewVersion.Engine;
using LzAssessor.NewVersion.Models;
using LzAssessor.NewVersion.Persistence;
using LzAssessor.NewVersion.Specs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace LzAssessor.NewVersion.Triggers;

/// <summary>
/// Simplified triggers without Durable Functions for initial implementation
/// </summary>
public static class SimplifiedTriggers
{
    /// <summary>
    /// Run assessment directly (without orchestration)
    /// </summary>
    [Function(nameof(RunAssessmentDirect))]
    public static async Task<HttpResponseData> RunAssessmentDirect(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "assessment/run")] HttpRequestData req,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(RunAssessmentDirect));
        
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var request = string.IsNullOrEmpty(requestBody) 
                ? new AssessmentRequest() 
                : JsonSerializer.Deserialize<AssessmentRequest>(requestBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            var runId = string.IsNullOrEmpty(request?.RunId) ? Guid.NewGuid().ToString() : request.RunId;

            // Get services
            var discoveryService = context.InstanceServices.GetRequiredService<IDiscoveryService>();
            var specLoader = context.InstanceServices.GetRequiredService<ISpecLoader>();
            var router = context.InstanceServices.GetRequiredService<ExecutionRouter>();
            var persistence = context.InstanceServices.GetRequiredService<IAssessmentPersistence>();

            logger.LogInformation("Starting assessment run {RunId}", runId);

            // Discovery
            var snapshot = !string.IsNullOrEmpty(request?.TenantId) 
                ? await discoveryService.DiscoverAsync(request.TenantId)
                : await discoveryService.DiscoverAsync();
            
            if (!string.IsNullOrEmpty(request?.TenantId))
            {
                logger.LogInformation("Using specified tenant {TenantId} with {SubscriptionCount} subscriptions", 
                    snapshot.TenantId, snapshot.Subscriptions.Length);
            }
            else
            {
                logger.LogInformation("Discovered tenant {TenantId} with {SubscriptionCount} subscriptions", 
                    snapshot.TenantId, snapshot.Subscriptions.Length);
            }

            // Load spec
            var spec = await specLoader.LoadAsync(request?.SpecUrl);
            logger.LogInformation("Loaded spec {SpecVersion} with {CheckCount} checks", 
                spec.SpecVersion, spec.Checks.Count);

            // Execute checks
            var results = await router.ExecuteChecksAsync(
                runId, 
                snapshot.TenantId, 
                request?.Subscriptions?.FirstOrDefault() ?? "tenant", 
                spec.Checks, 
                snapshot, 
                maxConcurrency: 3);

            logger.LogInformation("Executed {CheckCount} checks", results.Length);

            // Create assessment run
            var run = new AssessmentRun(
                RunId: runId,
                TenantId: snapshot.TenantId,
                SpecVersion: spec.SpecVersion,
                Category: spec.Category,
                Results: results,
                Snapshot: snapshot,
                StartedAt: DateTimeOffset.UtcNow.AddMinutes(-5), // Approximate start time
                CompletedAt: DateTimeOffset.UtcNow,
                TotalChecks: results.Length,
                CompliantChecks: results.Count(r => r.Status == AssessmentStatus.Compliant),
                NonCompliantChecks: results.Count(r => r.Status == AssessmentStatus.NonCompliant),
                ManualChecks: results.Count(r => r.Status == AssessmentStatus.ManualRequired));

            // Persist results
            await persistence.SaveAssessmentRunAsync(run);

            logger.LogInformation("Assessment run {RunId} completed with {CompliantCount}/{TotalCount} compliant checks", 
                runId, run.CompliantChecks, run.TotalChecks);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(run);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run assessment");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message, details = ex.ToString() });
            return errorResponse;
        }
    }

    /// <summary>
    /// Get assessment history
    /// </summary>
    [Function(nameof(GetAssessmentHistory))]
    public static async Task<HttpResponseData> GetAssessmentHistory(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "assessment/history")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var tenantId = query["tenantId"];
            var maxRuns = int.TryParse(query["maxRuns"], out var max) ? max : 10;

            if (string.IsNullOrEmpty(tenantId))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteAsJsonAsync(new { error = "tenantId parameter is required" });
                return badRequestResponse;
            }

            var persistence = context.InstanceServices.GetRequiredService<IAssessmentPersistence>();
            var history = await persistence.GetAssessmentHistoryAsync(tenantId, maxRuns);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(history);

            return response;
        }
        catch (Exception ex)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}