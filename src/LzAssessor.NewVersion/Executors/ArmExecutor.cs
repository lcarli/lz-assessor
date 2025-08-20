using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using LzAssessor.NewVersion.Engine;
using LzAssessor.NewVersion.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace LzAssessor.NewVersion.Executors;

/// <summary>
/// Executor for Azure Resource Manager checks
/// </summary>
public sealed class ArmExecutor : ExecutorBase
{
    private readonly ArmClient _armClient;

    public override string Name => "arm";

    public ArmExecutor()
    {
        var credential = new DefaultAzureCredential();
        _armClient = new ArmClient(credential);
    }

    public override async Task<AssessmentResult> ExecuteAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build payload by fetching data from specified sources
            var payload = new JObject();

            foreach (var source in check.Sources)
            {
                var sourceData = await FetchSourceDataAsync(source, snapshot, cancellationToken);
                payload.Merge(sourceData, new JsonMergeSettings 
                { 
                    MergeArrayHandling = MergeArrayHandling.Union 
                });
            }

            // Evaluate logic against payload
            var isCompliant = LogicEvaluator.Evaluate(payload, check.Logic);
            var status = isCompliant ? AssessmentStatus.Compliant : AssessmentStatus.NonCompliant;

            // Build evidence
            var evidence = BuildEvidence(check, payload);

            return new AssessmentResult(
                RunId: runId,
                TenantId: tenantId,
                Scope: scope,
                QuestionId: check.Id,
                Title: check.Title,
                Pillar: "Governance",
                Status: status,
                Severity: check.Severity,
                Coverage: "Auto",
                Evidence: evidence,
                Metadata: new Dictionary<string, object?>
                {
                    ["executor"] = Name,
                    ["sources"] = check.Sources,
                    ["subscriptions"] = snapshot.Subscriptions.Length
                },
                EvaluatedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return CreateErrorResult(runId, tenantId, scope, check, ex);
        }
    }

    private async Task<JObject> FetchSourceDataAsync(
        string source, 
        DiscoverySnapshot snapshot, 
        CancellationToken cancellationToken)
    {
        var result = new JObject();

        try
        {
            switch (source)
            {
                case "ARM.Resources":
                    result["resources"] = await GetResourcesAsync(snapshot, cancellationToken);
                    break;

                case "ARM.RoleAssignments":
                    result["roleAssignments"] = await GetRoleAssignmentsAsync(snapshot, cancellationToken);
                    break;

                case "ARM.ResourceLocks":
                    result["resourceLocks"] = await GetResourceLocksAsync(snapshot, cancellationToken);
                    break;

                case "ARM.DiagnosticSettings":
                    result["diagnosticSettings"] = await GetDiagnosticSettingsAsync(snapshot, cancellationToken);
                    break;

                default:
                    result[source.Replace(".", "_")] = new JObject { ["error"] = "Unknown source" };
                    break;
            }
        }
        catch (Exception ex)
        {
            result[source.Replace(".", "_")] = new JObject { ["error"] = ex.Message };
        }

        return result;
    }

    private async Task<JArray> GetResourcesAsync(DiscoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        var resources = new JArray();

        foreach (var subscriptionId in snapshot.Subscriptions)
        {
            try
            {
                var subscription = _armClient.GetSubscriptionResource(
                    ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}"));
                
                await foreach (var resourceGroup in subscription.GetResourceGroups().GetAllAsync(cancellationToken: cancellationToken))
                {
                    var resourceCollection = resourceGroup.GetGenericResources();
                    foreach (var resource in resourceCollection)
                    {
                        resources.Add(JObject.FromObject(new
                        {
                            id = resource.Id.ToString(),
                            name = resource.Data.Name,
                            type = resource.Data.ResourceType.ToString(),
                            location = resource.Data.Location.Name,
                            resourceGroup = resourceGroup.Data.Name,
                            subscription = subscriptionId,
                            tags = resource.Data.Tags
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error for this subscription but continue with others
                resources.Add(JObject.FromObject(new
                {
                    subscription = subscriptionId,
                    error = ex.Message
                }));
            }
        }

        return resources;
    }

    private Task<JArray> GetRoleAssignmentsAsync(DiscoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        var assignments = new JArray();

        foreach (var subscriptionId in snapshot.Subscriptions)
        {
            try
            {
                // Simplified - in practice you'd use authorization management API
                assignments.Add(JObject.FromObject(new
                {
                    subscription = subscriptionId,
                    roleAssignments = Array.Empty<object>(),
                    note = "Role assignment collection requires Authorization Management API"
                }));
            }
            catch (Exception ex)
            {
                assignments.Add(JObject.FromObject(new
                {
                    subscription = subscriptionId,
                    error = ex.Message
                }));
            }
        }

        return Task.FromResult(assignments);
    }

    private async Task<JArray> GetResourceLocksAsync(DiscoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        var locks = new JArray();

        foreach (var subscriptionId in snapshot.Subscriptions)
        {
            try
            {
                var subscription = _armClient.GetSubscriptionResource(
                    ResourceIdentifier.Parse($"/subscriptions/{subscriptionId}"));
                
                await foreach (var lockResource in subscription.GetManagementLocks().GetAllAsync(cancellationToken: cancellationToken))
                {
                    locks.Add(JObject.FromObject(new
                    {
                        id = lockResource.Id.ToString(),
                        name = lockResource.Data.Name,
                        level = lockResource.Data.Level.ToString(),
                        notes = lockResource.Data.Notes,
                        subscription = subscriptionId
                    }));
                }
            }
            catch (Exception ex)
            {
                locks.Add(JObject.FromObject(new
                {
                    subscription = subscriptionId,
                    error = ex.Message
                }));
            }
        }

        return locks;
    }

    private Task<JArray> GetDiagnosticSettingsAsync(DiscoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        // This is a placeholder - diagnostic settings require resource-specific queries
        var settings = new JArray();
        
        // Add placeholder data
        foreach (var subscriptionId in snapshot.Subscriptions)
        {
            settings.Add(JObject.FromObject(new
            {
                subscription = subscriptionId,
                diagnosticSettings = Array.Empty<object>(),
                note = "Diagnostic settings collection not fully implemented"
            }));
        }

        return Task.FromResult(settings);
    }

    private static Evidence BuildEvidence(SpecCheck check, JObject payload)
    {
        var summary = BuildSummary(check, payload);
        return new Evidence(
            Summary: summary,
            Links: check.Evidence.LinkTemplates);
    }

    private static string BuildSummary(SpecCheck check, JObject payload)
    {
        return check.Id switch
        {
            _ => $"{check.Title} evaluated with ARM data."
        };
    }
}