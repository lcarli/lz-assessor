using LzAssessor.NewVersion.Executors;
using LzAssessor.NewVersion.Models;

namespace LzAssessor.NewVersion.Engine;

/// <summary>
/// Routes checks to appropriate executors based on the executor name
/// </summary>
public sealed class ExecutionRouter
{
    private readonly Dictionary<string, IExecutor> _executors;

    public ExecutionRouter(IEnumerable<IExecutor> executors)
    {
        _executors = executors.ToDictionary(e => e.Name, e => e, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Execute a single check using the appropriate executor
    /// </summary>
    public async Task<AssessmentResult> ExecuteCheckAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        if (!_executors.TryGetValue(check.Executor, out var executor))
        {
            return CreateUnsupportedExecutorResult(runId, tenantId, scope, check);
        }

        return await executor.ExecuteAsync(runId, tenantId, scope, check, snapshot, cancellationToken);
    }

    /// <summary>
    /// Execute multiple checks in parallel
    /// </summary>
    public async Task<AssessmentResult[]> ExecuteChecksAsync(
        string runId,
        string tenantId,
        string scope,
        IEnumerable<SpecCheck> checks,
        DiscoverySnapshot snapshot,
        int maxConcurrency = 5,
        CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = checks.Select(async check =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteCheckAsync(runId, tenantId, scope, check, snapshot, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Get available executor names
    /// </summary>
    public IReadOnlyCollection<string> GetAvailableExecutors()
    {
        return _executors.Keys;
    }

    private static AssessmentResult CreateUnsupportedExecutorResult(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check)
    {
        return new AssessmentResult(
            RunId: runId,
            TenantId: tenantId,
            Scope: scope,
            QuestionId: check.Id,
            Title: check.Title,
            Pillar: "General",
            Status: AssessmentStatus.Error,
            Severity: check.Severity,
            Coverage: "Auto",
            Evidence: new Evidence(
                Summary: $"Unsupported executor: {check.Executor}",
                Links: check.Evidence.LinkTemplates),
            Metadata: new Dictionary<string, object?>
            {
                ["executor"] = check.Executor,
                ["error"] = "Executor not found"
            },
            EvaluatedAt: DateTimeOffset.UtcNow);
    }
}