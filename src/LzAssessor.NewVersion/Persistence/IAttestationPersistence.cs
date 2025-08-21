using LzAssessor.NewVersion.Models;

namespace LzAssessor.NewVersion.Persistence;

/// <summary>
/// Interface for persisting attestations
/// </summary>
public interface IAttestationPersistence
{
    /// <summary>
    /// Save an attestation
    /// </summary>
    Task SaveAttestationAsync(Attestation attestation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get attestations for a tenant and check
    /// </summary>
    Task<Attestation[]> GetAttestationsAsync(string tenantId, string checkId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all attestations for a tenant
    /// </summary>
    Task<Attestation[]> GetTenantAttestationsAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get latest valid attestation for a check
    /// </summary>
    Task<Attestation?> GetLatestValidAttestationAsync(string tenantId, string checkId, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory attestation persistence for development/testing
/// </summary>
public sealed class InMemoryAttestationPersistence : IAttestationPersistence
{
    private readonly List<Attestation> _attestations = new();
    private readonly object _lock = new();

    public Task SaveAttestationAsync(Attestation attestation, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _attestations.Add(attestation);
        }
        return Task.CompletedTask;
    }

    public Task<Attestation[]> GetAttestationsAsync(string tenantId, string checkId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var attestations = _attestations
                .Where(a => a.TenantId == tenantId && a.CheckId == checkId)
                .OrderByDescending(a => a.SubmittedAt)
                .ToArray();

            return Task.FromResult(attestations);
        }
    }

    public Task<Attestation[]> GetTenantAttestationsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var attestations = _attestations
                .Where(a => a.TenantId == tenantId)
                .OrderByDescending(a => a.SubmittedAt)
                .ToArray();

            return Task.FromResult(attestations);
        }
    }

    public Task<Attestation?> GetLatestValidAttestationAsync(string tenantId, string checkId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var attestation = _attestations
                .Where(a => a.TenantId == tenantId && 
                           a.CheckId == checkId && 
                           a.ExpiresAt > now)
                .OrderByDescending(a => a.SubmittedAt)
                .FirstOrDefault();

            return Task.FromResult(attestation);
        }
    }
}