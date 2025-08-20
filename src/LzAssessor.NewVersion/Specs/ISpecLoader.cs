using LzAssessor.NewVersion.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace LzAssessor.NewVersion.Specs;

/// <summary>
/// Interface for loading assessment specifications
/// </summary>
public interface ISpecLoader
{
    /// <summary>
    /// Load spec from URL or default location
    /// </summary>
    Task<SpecFile> LoadAsync(string? specUrl = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// HTTP-based spec loader
/// </summary>
public sealed class HttpSpecLoader : ISpecLoader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public HttpSpecLoader(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<SpecFile> LoadAsync(string? specUrl = null, CancellationToken cancellationToken = default)
    {
        var url = specUrl ?? _configuration["Assessment:SpecUrl"];
        
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Spec URL not provided and not configured in settings");
        }

        var httpClient = _httpClientFactory.CreateClient();
        
        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var spec = await JsonSerializer.DeserializeAsync<SpecFile>(stream, JsonOptions, cancellationToken);

        if (spec == null)
        {
            throw new InvalidOperationException($"Failed to deserialize spec from URL: {url}");
        }

        return spec;
    }
}

/// <summary>
/// File-based spec loader for local development
/// </summary>
public sealed class FileSpecLoader : ISpecLoader
{
    private readonly string _basePath;
    
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public FileSpecLoader(string basePath = "specs")
    {
        _basePath = basePath;
    }

    public async Task<SpecFile> LoadAsync(string? specUrl = null, CancellationToken cancellationToken = default)
    {
        var fileName = specUrl ?? "billing_entra.json";
        var filePath = Path.Combine(_basePath, fileName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Spec file not found: {filePath}");
        }

        using var stream = File.OpenRead(filePath);
        var spec = await JsonSerializer.DeserializeAsync<SpecFile>(stream, JsonOptions, cancellationToken);

        if (spec == null)
        {
            throw new InvalidOperationException($"Failed to deserialize spec from file: {filePath}");
        }

        return spec;
    }
}