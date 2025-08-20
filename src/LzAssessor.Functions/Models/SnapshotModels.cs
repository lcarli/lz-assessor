namespace LzAssessor.Functions.Models;

public record DiscoverySnapshot(
    string TenantId,
    string[] Subscriptions,
    DateTimeOffset CapturedAt);