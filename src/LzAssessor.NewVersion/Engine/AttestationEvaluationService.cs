using LzAssessor.NewVersion.Models;
using LzAssessor.NewVersion.Persistence;

namespace LzAssessor.NewVersion.Engine;

/// <summary>
/// Service for evaluating and applying attestations to assessment results
/// </summary>
public interface IAttestationEvaluationService
{
    /// <summary>
    /// Apply attestations to assessment results
    /// </summary>
    Task<AssessmentResult[]> ApplyAttestationsAsync(
        string tenantId, 
        AssessmentResult[] results, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of attestation evaluation service
/// </summary>
public sealed class AttestationEvaluationService : IAttestationEvaluationService
{
    private readonly IAttestationPersistence _attestationPersistence;

    public AttestationEvaluationService(IAttestationPersistence attestationPersistence)
    {
        _attestationPersistence = attestationPersistence;
    }

    public async Task<AssessmentResult[]> ApplyAttestationsAsync(
        string tenantId, 
        AssessmentResult[] results, 
        CancellationToken cancellationToken = default)
    {
        var enhancedResults = new List<AssessmentResult>();

        foreach (var result in results)
        {
            // Only apply attestations to ManualRequired checks
            if (result.Status == AssessmentStatus.ManualRequired)
            {
                var attestation = await _attestationPersistence.GetLatestValidAttestationAsync(
                    tenantId, result.QuestionId, cancellationToken);

                if (attestation != null)
                {
                    // Create new result with attestation-based status
                    var attestedStatus = ConvertAttestationStatus(attestation.Status);
                    var enhancedEvidence = new Evidence(
                        Summary: $"Manual attestation: {attestation.Comments}",
                        Links: result.Evidence.Links,
                        RawRef: result.Evidence.RawRef);

                    var enhancedMetadata = new Dictionary<string, object?>(result.Metadata)
                    {
                        ["attestation"] = new
                        {
                            id = attestation.Id,
                            attestorName = attestation.AttestorName,
                            attestorEmail = attestation.AttestorEmail,
                            submittedAt = attestation.SubmittedAt,
                            expiresAt = attestation.ExpiresAt,
                            evidence = attestation.Evidence
                        }
                    };

                    var enhancedResult = new AssessmentResult(
                        RunId: result.RunId,
                        TenantId: result.TenantId,
                        Scope: result.Scope,
                        QuestionId: result.QuestionId,
                        Title: result.Title,
                        Pillar: result.Pillar,
                        Status: attestedStatus,
                        Severity: result.Severity,
                        Coverage: "Manual", // Override to Manual since it's attestation-based
                        Evidence: enhancedEvidence,
                        Metadata: enhancedMetadata,
                        EvaluatedAt: result.EvaluatedAt);

                    enhancedResults.Add(enhancedResult);
                }
                else
                {
                    // No valid attestation found, keep original result
                    enhancedResults.Add(result);
                }
            }
            else
            {
                // Not a manual check, keep original result
                enhancedResults.Add(result);
            }
        }

        return enhancedResults.ToArray();
    }

    private static AssessmentStatus ConvertAttestationStatus(AttestationStatus attestationStatus)
    {
        return attestationStatus switch
        {
            AttestationStatus.Compliant => AssessmentStatus.Compliant,
            AttestationStatus.NonCompliant => AssessmentStatus.NonCompliant,
            AttestationStatus.NotApplicable => AssessmentStatus.NotApplicable,
            AttestationStatus.Exempted => AssessmentStatus.Exempted,
            _ => AssessmentStatus.ManualRequired
        };
    }
}