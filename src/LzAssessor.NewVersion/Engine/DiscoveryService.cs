using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using LzAssessor.NewVersion.Models;
using Microsoft.Graph;

namespace LzAssessor.NewVersion.Engine;

/// <summary>
/// Service for discovering tenant and subscription information
/// </summary>
public interface IDiscoveryService
{
    /// <summary>
    /// Discover tenant and subscription information
    /// </summary>
    Task<DiscoverySnapshot> DiscoverAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Discover tenant and subscription information for a specific tenant
    /// </summary>
    Task<DiscoverySnapshot> DiscoverAsync(string tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure-based discovery service
/// </summary>
public sealed class AzureDiscoveryService : IDiscoveryService
{
    private readonly ArmClient _armClient;
    private readonly GraphServiceClient _graphClient;

    public AzureDiscoveryService()
    {
        var credential = new DefaultAzureCredential();
        _armClient = new ArmClient(credential);
        _graphClient = new GraphServiceClient(credential);
    }

    public async Task<DiscoverySnapshot> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = await GetTenantIdAsync(cancellationToken);
        var subscriptions = await GetSubscriptionsAsync(cancellationToken);
        var metadata = await GetMetadataAsync(cancellationToken);

        return new DiscoverySnapshot(
            TenantId: tenantId,
            Subscriptions: subscriptions,
            Metadata: metadata,
            CapturedAt: DateTimeOffset.UtcNow);
    }

    public async Task<DiscoverySnapshot> DiscoverAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        // When a specific tenant ID is provided, we still discover subscriptions and metadata
        // but use the provided tenant ID instead of discovering it
        var subscriptions = await GetSubscriptionsAsync(cancellationToken);
        var metadata = await GetMetadataAsync(cancellationToken);

        return new DiscoverySnapshot(
            TenantId: tenantId,
            Subscriptions: subscriptions,
            Metadata: metadata,
            CapturedAt: DateTimeOffset.UtcNow);
    }

    private async Task<string> GetTenantIdAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get tenant information from Graph
            var organization = await _graphClient.Organization.GetAsync(cancellationToken: cancellationToken);
            var tenant = organization?.Value?.FirstOrDefault();
            
            if (tenant?.Id != null)
            {
                return tenant.Id;
            }

            // Fallback: extract from ARM client context
            var subscriptions = _armClient.GetSubscriptions();
            await foreach (var subscription in subscriptions.GetAllAsync(cancellationToken: cancellationToken))
            {
                // Extract tenant ID from subscription
                var subscriptionData = subscription.Data;
                return subscriptionData.TenantId?.ToString() ?? string.Empty;
            }

            throw new InvalidOperationException("Unable to determine tenant ID");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to discover tenant ID", ex);
        }
    }

    private async Task<string[]> GetSubscriptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var subscriptionIds = new List<string>();
            
            await foreach (var subscription in _armClient.GetSubscriptions().GetAllAsync(cancellationToken: cancellationToken))
            {
                subscriptionIds.Add(subscription.Data.SubscriptionId);
            }

            return subscriptionIds.ToArray();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to discover subscriptions", ex);
        }
    }

    private async Task<Dictionary<string, object>> GetMetadataAsync(CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, object>();

        try
        {
            // Get basic tenant information
            var organization = await _graphClient.Organization.GetAsync(cancellationToken: cancellationToken);
            var tenant = organization?.Value?.FirstOrDefault();
            
            if (tenant != null)
            {
                metadata["tenantName"] = tenant.DisplayName ?? "Unknown";
                metadata["tenantType"] = tenant.TenantType ?? "Unknown";
                metadata["verifiedDomains"] = tenant.VerifiedDomains?.Count() ?? 0;
            }

            // Get subscription count and types
            var subscriptions = new List<object>();
            await foreach (var subscription in _armClient.GetSubscriptions().GetAllAsync(cancellationToken: cancellationToken))
            {
                subscriptions.Add(new
                {
                    id = subscription.Data.SubscriptionId,
                    name = subscription.Data.DisplayName,
                    state = subscription.Data.State?.ToString()
                });
            }
            
            metadata["subscriptions"] = subscriptions;
            metadata["subscriptionCount"] = subscriptions.Count;
        }
        catch (Exception ex)
        {
            metadata["discoveryError"] = ex.Message;
        }

        return metadata;
    }
}