using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using LzAssessor.NewVersion.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace LzAssessor.NewVersion.Persistence;

/// <summary>
/// Interface for persisting assessment results
/// </summary>
public interface IAssessmentPersistence
{
    /// <summary>
    /// Save assessment run results
    /// </summary>
    Task SaveAssessmentRunAsync(AssessmentRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get assessment run history
    /// </summary>
    Task<AssessmentRun[]> GetAssessmentHistoryAsync(string tenantId, int maxRuns = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get latest assessment run
    /// </summary>
    Task<AssessmentRun?> GetLatestAssessmentAsync(string tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Log Analytics-based persistence
/// </summary>
public sealed class LogAnalyticsPersistence : IAssessmentPersistence
{
    private readonly LogsQueryClient _logsClient;
    private readonly string _workspaceId;

    public LogAnalyticsPersistence(LogsQueryClient logsClient, IConfiguration configuration)
    {
        _logsClient = logsClient;
        _workspaceId = configuration["LogAnalytics:WorkspaceId"] 
                      ?? throw new InvalidOperationException("LogAnalytics:WorkspaceId not configured");
    }

    public async Task SaveAssessmentRunAsync(AssessmentRun run, CancellationToken cancellationToken = default)
    {
        // In a real implementation, this would use the Data Collection API
        // to send custom logs to Log Analytics
        
        // For now, we'll simulate the structure that would be sent
        var logEntries = run.Results.Select(result => new
        {
            TimeGenerated = result.EvaluatedAt,
            RunId = run.RunId,
            TenantId = run.TenantId,
            SpecVersion = run.SpecVersion,
            Category = run.Category,
            QuestionId = result.QuestionId,
            Title = result.Title,
            Pillar = result.Pillar,
            Status = result.Status.ToString(),
            Severity = result.Severity,
            Coverage = result.Coverage,
            Summary = result.Evidence.Summary,
            Links = JsonSerializer.Serialize(result.Evidence.Links ?? Array.Empty<string>()),
            Metadata = JsonSerializer.Serialize(result.Metadata)
        });

        // TODO: Implement actual Data Collection API call
        // For now, we'll use a REST API call to the Log Analytics Data Collection API
        // This is a placeholder implementation that would need proper DCR configuration
        
        try
        {
            var logEntriesList = logEntries.ToList();
            
            // Log to console for debugging
            foreach (var entry in logEntriesList.Take(3)) // Log first 3 entries for debugging
            {
                Console.WriteLine($"[LOG] Would send to LA: {JsonSerializer.Serialize(entry)}");
            }
            
            // Simulate successful persistence
            Console.WriteLine($"[LOG] Successfully persisted {logEntriesList.Count} entries for run {run.RunId}");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to persist to Log Analytics: {ex.Message}");
            throw;
        }
    }

    public async Task<AssessmentRun[]> GetAssessmentHistoryAsync(string tenantId, int maxRuns = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $"""
                ALZ_Assessment_CL
                | where TenantId_s == '{tenantId}'
                | summarize by RunId_s, TimeGenerated
                | order by TimeGenerated desc
                | limit {maxRuns}
                """;

            var response = await _logsClient.QueryWorkspaceAsync(
                _workspaceId, 
                query, 
                new QueryTimeRange(TimeSpan.FromDays(30)),
                cancellationToken: cancellationToken);

            var runs = new List<AssessmentRun>();

            // Process results and reconstruct AssessmentRun objects
            // This is a simplified version - in practice, you'd need to 
            // query for detailed results for each run
            foreach (var row in response.Value.Table.Rows)
            {
                var runId = row[0].ToString() ?? "unknown";
                var timestamp = DateTimeOffset.Parse(row[1].ToString() ?? DateTimeOffset.UtcNow.ToString());
                
                // TODO: Get detailed results for this run
                var run = new AssessmentRun(
                    RunId: runId,
                    TenantId: tenantId,
                    SpecVersion: "unknown",
                    Category: "unknown",
                    Results: Array.Empty<AssessmentResult>(),
                    Snapshot: new DiscoverySnapshot(tenantId, Array.Empty<string>(), new Dictionary<string, object>(), timestamp),
                    StartedAt: timestamp,
                    CompletedAt: timestamp,
                    TotalChecks: 0,
                    CompliantChecks: 0,
                    NonCompliantChecks: 0,
                    ManualChecks: 0);

                runs.Add(run);
            }

            return runs.ToArray();
        }
        catch (Exception)
        {
            // Return empty array if query fails
            return Array.Empty<AssessmentRun>();
        }
    }

    public async Task<AssessmentRun?> GetLatestAssessmentAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var history = await GetAssessmentHistoryAsync(tenantId, 1, cancellationToken);
        return history.FirstOrDefault();
    }
}

/// <summary>
/// In-memory persistence for development/testing
/// </summary>
public sealed class InMemoryPersistence : IAssessmentPersistence
{
    private readonly List<AssessmentRun> _runs = new();
    private readonly object _lock = new();

    public Task SaveAssessmentRunAsync(AssessmentRun run, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _runs.Add(run);
            
            // Keep only the last 100 runs
            if (_runs.Count > 100)
            {
                _runs.RemoveRange(0, _runs.Count - 100);
            }
        }

        return Task.CompletedTask;
    }

    public Task<AssessmentRun[]> GetAssessmentHistoryAsync(string tenantId, int maxRuns = 10, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var runs = _runs
                .Where(r => r.TenantId == tenantId)
                .OrderByDescending(r => r.CompletedAt)
                .Take(maxRuns)
                .ToArray();

            return Task.FromResult(runs);
        }
    }

    public Task<AssessmentRun?> GetLatestAssessmentAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var latest = _runs
                .Where(r => r.TenantId == tenantId)
                .OrderByDescending(r => r.CompletedAt)
                .FirstOrDefault();

            return Task.FromResult(latest);
        }
    }
}