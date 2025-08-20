using LzAssessor.Functions.Models;
using LzAssessor.Functions.Specs;

namespace LzAssessor.Functions.Engine;

public sealed class GraphExecutor : IExecutor
{
    public string Name => "graph";

    public Task<AssessmentResult> ExecuteAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken ct)
    {
        // TODO:
        // - obter token (DefaultAzureCredential) para Graph
        // - chamar endpoints (domains, policies, CA)
        // - avaliar check.Logic
        return Task.FromResult(Manual(runId, tenantId, scope, check, "Graph executor stub"));
    }

    private static AssessmentResult Manual(string runId, string tenantId, string scope, SpecCheck check, string reason) =>
        new(
            RunId: runId,
            TenantId: tenantId,
            Scope: scope,
            QuestionId: check.Id,
            Title: check.Title,
            Pillar: "Billing/Entra",
            Status: check.Fallback?.ManualRequired == true ? "ManualRequired" : "Error",
            Severity: check.Severity,
            Coverage: check.Fallback?.ManualRequired == true ? "Manual" : "Auto",
            Evidence: new Evidence(
                Summary: reason,
                Links: check.Evidence.LinkTemplates,
                RawRef: null),
            Metadata: new Dictionary<string, object?>
            {
                ["executor"] = Name,
                ["sources"] = check.Sources
            }!.ToDictionary(k => k.Key, v => (object)v.Value!),
            UpdatedAt: DateTimeOffset.UtcNow);
}