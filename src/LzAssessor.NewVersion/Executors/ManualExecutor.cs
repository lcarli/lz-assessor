using LzAssessor.NewVersion.Models;

namespace LzAssessor.NewVersion.Executors;

/// <summary>
/// Executor for manual assessment checks that require human evaluation
/// </summary>
public sealed class ManualExecutor : ExecutorBase
{
    public override string Name => "manual";

    public override Task<AssessmentResult> ExecuteAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        // For manual checks, always return ManualRequired status
        var result = new AssessmentResult(
            RunId: runId,
            TenantId: tenantId,
            Scope: scope,
            QuestionId: check.Id,
            Title: check.Title,
            Pillar: DeterminePillar(check),
            Status: AssessmentStatus.Manually, // Using the new status as requested
            Severity: check.Severity,
            Coverage: "Manual",
            Evidence: new Evidence(
                Summary: check.Fallback?.Reason ?? "Manual assessment required",
                Links: check.Evidence.LinkTemplates),
            Metadata: new Dictionary<string, object?>
            {
                ["executor"] = Name,
                ["sources"] = check.Sources,
                ["manualReason"] = check.Fallback?.Reason ?? "Manual assessment required"
            },
            EvaluatedAt: DateTimeOffset.UtcNow);

        return Task.FromResult(result);
    }

    private static string DeterminePillar(SpecCheck check)
    {
        // Enhanced pillar determination based on check ID prefix
        var id = check.Id.ToUpperInvariant();
        if (id.StartsWith("ENTRA-") || id.StartsWith("IAM-")) return "Identity";
        if (id.StartsWith("BILLING-") || id.StartsWith("COST-")) return "Billing";
        if (id.StartsWith("SECURITY-")) return "Security";
        if (id.StartsWith("GOVERNANCE-")) return "Governance";
        if (id.StartsWith("NETWORK-")) return "Network";
        if (id.StartsWith("COMPUTE-")) return "Compute";
        if (id.StartsWith("DEVOPS-")) return "DevOps";
        if (id.StartsWith("MANAGEMENT-")) return "Management";
        if (id.StartsWith("RESOURCE-ORG-")) return "ResourceOrganization";
        return "General";
    }
}