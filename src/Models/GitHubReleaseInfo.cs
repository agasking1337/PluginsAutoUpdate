namespace PluginsAutoUpdate;

/// <summary>
/// Represents information about a GitHub release.
/// </summary>
/// <param name="Version">The version of the release</param>
/// <param name="AssetUrl">URL to download the release asset</param>
public sealed record GitHubReleaseInfo(Version? Version, string? AssetUrl);
