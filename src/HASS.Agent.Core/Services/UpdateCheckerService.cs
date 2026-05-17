using System.Reflection;
using Octokit;
using Serilog;

namespace HASS.Agent.Core.Services;

/// <summary>
/// Checks the GitHub releases of the active fork (hass-agent/HASS.Agent) for a newer version
/// than the currently-running assembly. Cached for an hour so we don't hammer the API.
/// </summary>
public interface IUpdateCheckerService
{
    /// <summary>Latest available version on GitHub, or null if check failed / nothing newer.</summary>
    Task<UpdateInfo?> CheckForUpdateAsync(bool force = false);
}

public sealed record UpdateInfo(string LatestVersion, string CurrentVersion, string ReleaseUrl, string Notes);

public sealed class UpdateCheckerService : IUpdateCheckerService
{
    // Switch to your own repo if you want self-hosted updates.
    private const string GitHubOwner = "hass-agent";
    private const string GitHubRepo  = "HASS.Agent";

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    private UpdateInfo? _cached;
    private DateTime _cachedAt = DateTime.MinValue;

    public async Task<UpdateInfo?> CheckForUpdateAsync(bool force = false)
    {
        if (!force && _cached != null && (DateTime.Now - _cachedAt) < CacheLifetime)
            return _cached;

        try
        {
            var client = new GitHubClient(new ProductHeaderValue("HASS-Agent-WinUI"));
            var latest = await client.Repository.Release.GetLatest(GitHubOwner, GitHubRepo);
            var latestTag = NormaliseVersion(latest.TagName);

            var current = NormaliseVersion(
                Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ??
                "0.0.0");

            if (IsNewer(latestTag, current))
            {
                _cached = new UpdateInfo(latestTag, current, latest.HtmlUrl, latest.Body ?? string.Empty);
                _cachedAt = DateTime.Now;
                Log.Information("[UPDATE] New version available: {latest} (current {current})", latestTag, current);
                return _cached;
            }

            Log.Information("[UPDATE] Up to date — running {current}, latest {latest}", current, latestTag);
            _cached = null;
            _cachedAt = DateTime.Now;
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[UPDATE] Check failed: {err}", ex.Message);
            return null;
        }
    }

    private static string NormaliseVersion(string s) =>
        s.TrimStart('v', 'V').Split('-')[0]; // "v2.2.0-beta1" → "2.2.0"

    private static bool IsNewer(string latest, string current)
    {
        if (!Version.TryParse(Pad(latest), out var l)) return false;
        if (!Version.TryParse(Pad(current), out var c)) return false;
        return l > c;
    }

    private static string Pad(string s)
    {
        // Version.TryParse needs at least Major.Minor — pad single numbers.
        var parts = s.Split('.');
        return parts.Length switch
        {
            1 => $"{s}.0",
            _ => s
        };
    }
}
