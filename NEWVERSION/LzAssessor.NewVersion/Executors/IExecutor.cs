using LzAssessor.NewVersion.Models;

namespace LzAssessor.NewVersion.Executors;

/// <summary>
/// Interface for assessment executors that collect evidence and evaluate checks
/// </summary>
public interface IExecutor
{
    /// <summary>
    /// Name of this executor (matches SpecCheck.Executor)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Execute a single check and return the assessment result
    /// </summary>
    Task<AssessmentResult> ExecuteAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Base implementation with common functionality
/// </summary>
public abstract class ExecutorBase : IExecutor
{
    public abstract string Name { get; }

    public abstract Task<AssessmentResult> ExecuteAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a manual result when automatic assessment is not possible
    /// </summary>
    protected static AssessmentResult CreateManualResult(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        string reason)
    {
        return new AssessmentResult(
            RunId: runId,
            TenantId: tenantId,
            Scope: scope,
            QuestionId: check.Id,
            Title: check.Title,
            Pillar: DeterminePillar(check),
            Status: check.Fallback?.ManualRequired == true ? AssessmentStatus.ManualRequired : AssessmentStatus.Error,
            Severity: check.Severity,
            Coverage: check.Fallback?.ManualRequired == true ? "Manual" : "Auto",
            Evidence: new Evidence(
                Summary: reason,
                Links: check.Evidence.LinkTemplates),
            Metadata: new Dictionary<string, object?>
            {
                ["executor"] = "",
                ["sources"] = check.Sources,
                ["reason"] = reason
            },
            EvaluatedAt: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Create an error result when execution fails
    /// </summary>
    protected static AssessmentResult CreateErrorResult(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        Exception exception)
    {
        return new AssessmentResult(
            RunId: runId,
            TenantId: tenantId,
            Scope: scope,
            QuestionId: check.Id,
            Title: check.Title,
            Pillar: DeterminePillar(check),
            Status: AssessmentStatus.Error,
            Severity: check.Severity,
            Coverage: "Auto",
            Evidence: new Evidence(
                Summary: $"Error during execution: {exception.Message}",
                Links: check.Evidence.LinkTemplates),
            Metadata: new Dictionary<string, object?>
            {
                ["executor"] = "",
                ["sources"] = check.Sources,
                ["error"] = exception.ToString()
            },
            EvaluatedAt: DateTimeOffset.UtcNow);
    }

    private static string DeterminePillar(SpecCheck check)
    {
        // Basic pillar determination based on check ID prefix
        var id = check.Id.ToUpperInvariant();
        if (id.StartsWith("ENTRA-")) return "Identity";
        if (id.StartsWith("BILLING-") || id.StartsWith("COST-")) return "Billing";
        if (id.StartsWith("SECURITY-")) return "Security";
        if (id.StartsWith("GOVERNANCE-")) return "Governance";
        if (id.StartsWith("NETWORK-")) return "Network";
        if (id.StartsWith("COMPUTE-")) return "Compute";
        return "General";
    }
}