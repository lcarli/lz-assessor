using LzAssessor.Functions.Models;
using LzAssessor.Functions.Specs;

namespace LzAssessor.Functions.Engine;

public class ExecutionRouter
{
    private readonly Dictionary<string, IExecutor> _executors;

    public ExecutionRouter(IEnumerable<IExecutor> executors)
    {
        _executors = executors.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
    }

    public Task<AssessmentResult> RunAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken ct)
    {
        if (!_executors.TryGetValue(check.Executor, out var exec))
            throw new InvalidOperationException($"Executor '{check.Executor}' não registrado.");

        return exec.ExecuteAsync(runId, tenantId, scope, check, snapshot, ct);
    }
}