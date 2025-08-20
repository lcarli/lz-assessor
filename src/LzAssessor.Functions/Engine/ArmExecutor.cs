using System.Text.Json;
using LzAssessor.Functions.Models;
using LzAssessor.Functions.Specs;

namespace LzAssessor.Functions.Engine;

public sealed class ArmExecutor : IExecutor
{
    public string Name => "arm";

    public Task<AssessmentResult> ExecuteAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken ct)
    {
        // TODO: implementar:
        // - listar recursos/role assignments/locks/diag settings conforme "check.Sources"
        // - aplicar "check.Logic" (predicados declarativos)
        // por enquanto, devolve ManualRequired como placeholder
        return Task.FromResult(Manual(runId, tenantId, scope, check, "ARM executor stub"));
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