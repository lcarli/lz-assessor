using System.Net;

namespace LzAssessor.NewVersion.Engine;

/// <summary>
/// Retry policy for resilient API calls
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// Execute operation with exponential backoff retry
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        int baseDelayMs = 1000,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        
        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxRetries && ShouldRetry(ex))
            {
                attempt++;
                var delay = CalculateDelay(attempt, baseDelayMs);
                
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Execute operation with simple retry (no return value)
    /// </summary>
    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxRetries = 3,
        int baseDelayMs = 1000,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true; // Dummy return value
        }, maxRetries, baseDelayMs, cancellationToken);
    }

    private static bool ShouldRetry(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx => ShouldRetryHttpStatus(httpEx),
            TaskCanceledException => false, // Don't retry timeouts
            OperationCanceledException => false, // Don't retry cancellations
            _ => false // Default: don't retry other exceptions
        };
    }

    private static bool ShouldRetryHttpStatus(HttpRequestException httpEx)
    {
        // Extract status code from exception message if possible
        var message = httpEx.Message?.ToLowerInvariant() ?? "";
        
        // Retry on transient HTTP errors
        return message.Contains("429") ||   // Too Many Requests
               message.Contains("500") ||   // Internal Server Error
               message.Contains("502") ||   // Bad Gateway
               message.Contains("503") ||   // Service Unavailable
               message.Contains("504");     // Gateway Timeout
    }

    private static int CalculateDelay(int attempt, int baseDelayMs)
    {
        // Exponential backoff: base * 2^(attempt-1) with jitter
        var delay = baseDelayMs * Math.Pow(2, attempt - 1);
        
        // Add random jitter (±25%)
        var jitter = Random.Shared.NextDouble() * 0.5 - 0.25; // -0.25 to +0.25
        delay = delay * (1 + jitter);
        
        // Cap at 30 seconds
        return Math.Min((int)delay, 30_000);
    }
}