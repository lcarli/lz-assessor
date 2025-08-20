namespace LzAssessor.NewVersion.Models;

/// <summary>
/// Snapshot of discovered environment (tenant, subscriptions)
/// </summary>
public record DiscoverySnapshot(
    string TenantId,
    string[] Subscriptions,
    Dictionary<string, object> Metadata,
    DateTimeOffset CapturedAt);

/// <summary>
/// Request to start an assessment
/// </summary>
public record AssessmentRequest(
    string? SpecUrl = null,
    string? TenantId = null,
    string[]? Subscriptions = null,
    string RunId = "");

/// <summary>
/// Result of a single check evaluation
/// </summary>
public record AssessmentResult(
    string RunId,
    string TenantId,
    string Scope,
    string QuestionId,
    string Title,
    string Pillar,
    AssessmentStatus Status,
    string Severity,
    string Coverage,
    Evidence Evidence,
    Dictionary<string, object?> Metadata,
    DateTimeOffset EvaluatedAt);

/// <summary>
/// Assessment status values
/// </summary>
public enum AssessmentStatus
{
    Compliant,
    NonCompliant,
    ManualRequired,
    NotApplicable,
    Exempted,
    Error
}

/// <summary>
/// Evidence for an assessment result
/// </summary>
public record Evidence(
    string Summary,
    IEnumerable<string>? Links = null,
    string? RawRef = null);

/// <summary>
/// Complete assessment run result
/// </summary>
public record AssessmentRun(
    string RunId,
    string TenantId,
    string SpecVersion,
    string Category,
    AssessmentResult[] Results,
    DiscoverySnapshot Snapshot,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int TotalChecks,
    int CompliantChecks,
    int NonCompliantChecks,
    int ManualChecks);