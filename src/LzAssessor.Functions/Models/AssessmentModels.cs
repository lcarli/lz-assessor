namespace LzAssessor.Functions.Models;

public record AssessmentRequest(
    string? Scope = null,
    string? Category = null,
    string? SpecUrl = null);

public record AssessmentResult(
    string RunId,
    string TenantId,
    string Scope,
    string QuestionId,
    string Title,
    string Pillar,
    string Status,              // Compliant|NonCompliant|NotApplicable|ManualRequired|Exempted|Error
    string Severity,
    string Coverage,            // Auto|Manual
    Evidence Evidence,
    IDictionary<string, object>? Metadata,
    DateTimeOffset UpdatedAt);

public record Evidence(
    string Summary,
    IEnumerable<string>? Links,
    string? RawRef);