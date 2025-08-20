using LzAssessor.Functions.Models;
using LzAssessor.Functions.Specs;

namespace LzAssessor.Functions.Engine;

public interface IExecutor
{
    /// <summary>Nome do executor (arm, graph, cost, policy, defender, backup)</summary>
    string Name { get; }

    /// <summary>Executa a lógica do check e devolve um resultado normalizado.</summary>
    Task<AssessmentResult> ExecuteAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken ct);
}