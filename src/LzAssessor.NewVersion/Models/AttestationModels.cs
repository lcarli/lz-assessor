namespace LzAssessor.NewVersion.Models;

/// <summary>
/// Attestation for manual verification of a check
/// </summary>
public record Attestation(
    string Id,
    string TenantId,
    string CheckId,
    string AttestorName,
    string AttestorEmail,
    AttestationStatus Status,
    string Comments,
    DateTimeOffset SubmittedAt,
    DateTimeOffset ExpiresAt,
    string? Evidence = null,
    Dictionary<string, object?>? Metadata = null);

/// <summary>
/// Status of an attestation
/// </summary>
public enum AttestationStatus
{
    Compliant,
    NonCompliant,
    NotApplicable,
    Exempted
}

/// <summary>
/// Request to submit an attestation
/// </summary>
public record AttestationRequest(
    string TenantId,
    string CheckId,
    AttestationStatus Status,
    string Comments,
    string AttestorName,
    string AttestorEmail,
    string? Evidence = null,
    int ExpirationDays = 90);

/// <summary>
/// Enhanced assessment result that includes attestation information
/// </summary>
public record EnhancedAssessmentResult : AssessmentResult
{
    public Attestation? Attestation { get; init; }
    
    public EnhancedAssessmentResult(AssessmentResult result, Attestation? attestation = null) 
        : base(result.RunId, result.TenantId, result.Scope, result.QuestionId, result.Title, 
               result.Pillar, result.Status, result.Severity, result.Coverage, result.Evidence, 
               result.Metadata, result.EvaluatedAt)
    {
        Attestation = attestation;
    }
}