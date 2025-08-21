using System.Text.Json;

namespace LzAssessor.NewVersion.Models;

/// <summary>
/// Root spec file structure
/// </summary>
public record SpecFile(
    string SpecVersion,
    string Category,
    string Profile,
    SpecDefaults Defaults,
    IReadOnlyList<SpecCheck> Checks);

/// <summary>
/// Default settings for the spec
/// </summary>
public record SpecDefaults(
    string Scope,
    string Severity,
    string Coverage,
    SpecAttestation Attestation);

/// <summary>
/// Attestation configuration
/// </summary>
public record SpecAttestation(
    bool Enabled, 
    int ExpiresDays);

/// <summary>
/// Individual check definition
/// </summary>
public record SpecCheck(
    string Id,
    string Title,
    string Severity,
    string Executor,
    string[] Sources,
    string? Scope,
    JsonElement Logic,
    SpecEvidence Evidence,
    SpecFallback? Fallback,
    string? RemediationHint,
    Dictionary<string, JsonElement>? Metadata = null);

/// <summary>
/// Evidence configuration for a check
/// </summary>
public record SpecEvidence(
    string SummaryTemplate,
    IEnumerable<string>? LinkTemplates = null,
    IEnumerable<string>? Capture = null);

/// <summary>
/// Fallback configuration for manual checks
/// </summary>
public record SpecFallback(
    bool ManualRequired, 
    string? Reason = null);