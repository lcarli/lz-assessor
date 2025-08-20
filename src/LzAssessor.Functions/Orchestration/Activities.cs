using LzAssessor.Functions.Engine;
using LzAssessor.Functions.Models;
using LzAssessor.Functions.Specs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LzAssessor.Functions.Orchestration;

public class Activities
{
    private readonly ILogger _log;
    private readonly IConfiguration _cfg;
    private readonly IChecklistProvider _specs;
    private readonly ExecutionRouter _router;

    public Activities(ILoggerFactory lf, IConfiguration cfg, IChecklistProvider specs, ExecutionRouter router)
    {
        _log = lf.CreateLogger<Activities>();
        _cfg = cfg;
        _specs = specs;
        _router = router;
    }

    // 1) Descoberta/snapshot (mínimo por enquanto)
    [Function(nameof(DiscoverActivity))]
    public Task<DiscoverySnapshot> DiscoverActivity([ActivityTrigger] object? _)
    {
        // TODO: pegar tenantId via ARM/Graph com Managed Identity
        var tenantId = "00000000-0000-0000-0000-000000000000";
        var subs = Array.Empty<string>(); // depois populamos
        return Task.FromResult(new DiscoverySnapshot(tenantId, subs, DateTimeOffset.UtcNow));
    }

    // 2) Carregar spec
    [Function(nameof(LoadSpecActivity))]
    public async Task<SpecFile> LoadSpecActivity([ActivityTrigger] AssessmentRequest req)
    {
        var spec = await _specs.LoadAsync(req.SpecUrl, CancellationToken.None);
        return spec;
    }

    // 3) Executar check (usa router para achar executor)
    public record ExecuteCheckPayload(string RunId, DiscoverySnapshot Snapshot, SpecCheck Check, string? ScopeOverride);

    [Function(nameof(ExecuteCheckActivity))]
    public async Task<AssessmentResult> ExecuteCheckActivity([ActivityTrigger] ExecuteCheckPayload payload)
    {
        var scope = payload.ScopeOverride ?? _cfg["Assessment:DefaultScope"] ?? "/";
        var res = await _router.RunAsync(payload.RunId, payload.Snapshot.TenantId, scope, payload.Check, payload.Snapshot, CancellationToken.None);
        return res;
    }

    // 4) Persistir resultados (sink) – placeholder
    [Function(nameof(PersistResultsActivity))]
    public Task PersistResultsActivity([ActivityTrigger] IEnumerable<AssessmentResult> results)
    {
        _log.LogInformation("Persist {count} results (sink TBD)", results.Count());
        // TODO: enviar para Log Analytics / Blob / Table
        return Task.CompletedTask;
    }
}