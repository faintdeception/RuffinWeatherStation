using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace RuffinWeatherStation.Api.Services;

public sealed class NwsIconCacheService
{
    private const string CacheRoutePrefix = "/api/garden/icon-cache";
    private const int DefaultRetentionDays = 365 * 3;
    private const int DefaultCleanupIntervalHours = 24;
    private static readonly TimeSpan MinRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan MaxRetention = TimeSpan.FromDays(365 * 10);
    private static readonly TimeSpan MinCleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxCleanupInterval = TimeSpan.FromDays(7);

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _downloadLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cleanupGate = new();
    private readonly TimeSpan _cacheRetention;
    private readonly TimeSpan _cleanupInterval;
    private DateTime _nextCleanupUtc = DateTime.MinValue;

    public NwsIconCacheService(IHttpClientFactory httpClientFactory, IWebHostEnvironment hostEnvironment, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(NwsIconCacheService));
        _httpClient.Timeout = TimeSpan.FromSeconds(8);
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("RuffinWeatherStation/1.0 (+https://ruffinweather.com)");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));

        var configuredRetentionDays = configuration.GetValue<int?>("NwsIconCache:RetentionDays") ?? DefaultRetentionDays;
        var configuredCleanupIntervalHours = configuration.GetValue<int?>("NwsIconCache:CleanupIntervalHours") ?? DefaultCleanupIntervalHours;

        _cacheRetention = Clamp(TimeSpan.FromDays(configuredRetentionDays), MinRetention, MaxRetention);
        _cleanupInterval = Clamp(TimeSpan.FromHours(configuredCleanupIntervalHours), MinCleanupInterval, MaxCleanupInterval);

        _cacheDirectory = Path.Combine(hostEnvironment.ContentRootPath, "icon-cache", "nws");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<string> GetCachedIconUrlAsync(string? sourceUrl)
    {
        TriggerCleanupIfDue();

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return string.Empty;
        }

        var normalized = sourceUrl.Trim();
        if (normalized.StartsWith(CacheRoutePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Host, "api.weather.gov", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var cacheKey = ComputeHash(normalized);
        var existingFilePath = FindCachedFilePath(cacheKey);
        if (existingFilePath != null)
        {
            Touch(existingFilePath);
            return BuildCacheRouteFromPath(existingFilePath);
        }

        var gate = _downloadLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            existingFilePath = FindCachedFilePath(cacheKey);
            if (existingFilePath != null)
            {
                Touch(existingFilePath);
                return BuildCacheRouteFromPath(existingFilePath);
            }

            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[NWS ICON CACHE] Download failed: {(int)response.StatusCode} {response.ReasonPhrase} for {uri}");
                return string.Empty;
            }

            var extension = ResolveExtension(response.Content.Headers.ContentType?.MediaType);
            var finalFilePath = Path.Combine(_cacheDirectory, $"{cacheKey}{extension}");
            var tempFilePath = Path.Combine(_cacheDirectory, $"{cacheKey}.{Guid.NewGuid():N}.tmp");

            await using (var sourceStream = await response.Content.ReadAsStreamAsync())
            await using (var targetStream = File.Create(tempFilePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            if (!File.Exists(finalFilePath))
            {
                File.Move(tempFilePath, finalFilePath);
                Touch(finalFilePath);
            }
            else
            {
                File.Delete(tempFilePath);
                Touch(finalFilePath);
            }

            return BuildCacheRouteFromPath(finalFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NWS ICON CACHE] Exception while caching icon {sourceUrl}: {ex.Message}");
            return string.Empty;
        }
        finally
        {
            gate.Release();
        }
    }

    private void TriggerCleanupIfDue()
    {
        var nowUtc = DateTime.UtcNow;
        lock (_cleanupGate)
        {
            if (nowUtc < _nextCleanupUtc)
            {
                return;
            }

            _nextCleanupUtc = nowUtc.Add(_cleanupInterval);
        }

        _ = Task.Run(CleanupExpiredFiles);
    }

    private void CleanupExpiredFiles()
    {
        try
        {
            var cutoff = DateTime.UtcNow.Subtract(_cacheRetention);
            foreach (var filePath in Directory.GetFiles(_cacheDirectory))
            {
                try
                {
                    var lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
                    if (lastWriteUtc < cutoff)
                    {
                        File.Delete(filePath);
                    }
                }
                catch
                {
                    // Ignore individual file failures so cleanup can continue.
                }
            }
        }
        catch
        {
            // Ignore cleanup failures and continue serving cache results.
        }
    }

    private static void Touch(string filePath)
    {
        try
        {
            File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow);
        }
        catch
        {
            // Non-fatal: cache entry remains valid even if timestamp update fails.
        }
    }

    public string? ResolveCachedIconFilePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var safeName = Path.GetFileName(fileName);
        if (!string.Equals(safeName, fileName, StringComparison.Ordinal))
        {
            return null;
        }

        var fullPath = Path.Combine(_cacheDirectory, safeName);
        return File.Exists(fullPath) ? fullPath : null;
    }

    private string? FindCachedFilePath(string cacheKey)
    {
        var matches = Directory.GetFiles(_cacheDirectory, $"{cacheKey}.*");
        return matches.FirstOrDefault();
    }

    private static string BuildCacheRouteFromPath(string filePath)
    {
        return $"{CacheRoutePrefix}/{Path.GetFileName(filePath)}";
    }

    private static string ResolveExtension(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return ".img";
        }

        return mediaType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            _ => ".img"
        };
    }

    private static string ComputeHash(string value)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
