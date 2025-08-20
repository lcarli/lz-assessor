using Azure.Core;
using Azure.Identity;
using LzAssessor.NewVersion.Engine;
using LzAssessor.NewVersion.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace LzAssessor.NewVersion.Executors;

/// <summary>
/// Executor for Azure Cost Management checks
/// </summary>
public sealed class CostExecutor : ExecutorBase
{
    private readonly TokenCredential _credential;

    public override string Name => "cost";

    public CostExecutor()
    {
        _credential = new DefaultAzureCredential();
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
                Pillar: "Billing",
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
                case "Cost.Budgets":
                    result["budgets"] = await GetBudgetsAsync(snapshot, cancellationToken);
                    break;

                case "Cost.Exports":
                    result["exports"] = await GetExportsAsync(snapshot, cancellationToken);
                    break;

                case "Cost.Reservations":
                    result["reservations"] = await GetReservationsAsync(snapshot, cancellationToken);
                    break;

                case "Cost.SavingsPlans":
                    result["savingsPlans"] = await GetSavingsPlansAsync(snapshot, cancellationToken);
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

    private Task<JArray> GetBudgetsAsync(DiscoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        var budgets = new JArray();

        // This is a placeholder implementation
        // In a real implementation, you would use the Cost Management REST API
        foreach (var subscriptionId in snapshot.Subscriptions)
        {
            try
            {
                // Placeholder: Cost Management API would be called here
                budgets.Add(JObject.FromObject(new
                {
                    subscription = subscriptionId,
                    budgets = Array.Empty<object>(),
                    note = "Budget collection not fully implemented - requires Cost Management API integration"
                }));
            }
            catch (Exception ex)
            {
                budgets.Add(JObject.FromObject(new
                {
                    subscription = subscriptionId,
                    error = ex.Message
                }));
            }
        }

        return Task.FromResult(budgets);
    }

    private Task<JArray> GetExportsAsync(DiscoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        var exports = new JArray();

        foreach (var subscriptionId in snapshot.Subscriptions)
        {
            try
            {
                // Placeholder: Cost Management API would be called here
                exports.Add(JObject.FromObject(new
                {
                    subscription = subscriptionId,
                    exports = Array.Empty<object>(),
                    note = "Export collection not fully implemented - requires Cost Management API integration"
                }));
            }
            catch (Exception ex)
            {
                exports.Add(JObject.FromObject(new
                {
                    subscription = subscriptionId,
                    error = ex.Message
                }));
            }
        }

        return Task.FromResult(exports);
    }

    private Task<JArray> GetReservationsAsync(DiscoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        var reservations = new JArray();

        try
        {
            // Placeholder: Reservations API would be called here
            reservations.Add(JObject.FromObject(new
            {
                tenantId = snapshot.TenantId,
                reservations = Array.Empty<object>(),
                note = "Reservations collection not fully implemented - requires Reservations API integration"
            }));
        }
        catch (Exception ex)
        {
            reservations.Add(JObject.FromObject(new
            {
                tenantId = snapshot.TenantId,
                error = ex.Message
            }));
        }

        return Task.FromResult(reservations);
    }

    private Task<JArray> GetSavingsPlansAsync(DiscoverySnapshot snapshot, CancellationToken cancellationToken)
    {
        var savingsPlans = new JArray();

        try
        {
            // Placeholder: Savings Plans API would be called here
            savingsPlans.Add(JObject.FromObject(new
            {
                tenantId = snapshot.TenantId,
                savingsPlans = Array.Empty<object>(),
                note = "Savings Plans collection not fully implemented - requires Savings Plans API integration"
            }));
        }
        catch (Exception ex)
        {
            savingsPlans.Add(JObject.FromObject(new
            {
                tenantId = snapshot.TenantId,
                error = ex.Message
            }));
        }

        return Task.FromResult(savingsPlans);
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
            _ => $"{check.Title} evaluated with Cost Management data."
        };
    }
}