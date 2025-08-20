namespace LzAssessor.Functions.Specs;

public record SpecFile(
    string SpecVersion,
    string Category,
    string Profile,
    SpecDefaults Defaults,
    IReadOnlyList<SpecCheck> Checks);

public record SpecDefaults(
    string Scope,
    string Severity,
    string Coverage,
    SpecAttestation Attestation);

public record SpecAttestation(bool Enabled, int ExpiresDays);

public record SpecCheck(
    string Id,
    string Title,
    string Severity,
    string Executor,             // "arm" | "graph" | "cost" | "policy" | "defender" | "backup"
    string[] Sources,
    string? Scope,
    object Logic,                // mantemos como object p/ passar "cru" ao executor
    SpecEvidence Evidence,
    SpecFallback? Fallback,
    string? RemediationHint);

public record SpecEvidence(
    string SummaryTemplate,
    IEnumerable<string>? LinkTemplates,
    IEnumerable<string>? Capture);

public record SpecFallback(bool ManualRequired, string? Reason);