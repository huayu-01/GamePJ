using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class UpdateManifest
{
    [JsonPropertyName("manifest_version")]
    public int ManifestVersion { get; set; } = 1;

    [JsonPropertyName("latest_app_version")]
    public string LatestAppVersion { get; set; } = Constants.AppVersion;

    [JsonPropertyName("minimum_app_version")]
    public string MinimumAppVersion { get; set; } = Constants.AppVersion;

    [JsonPropertyName("protocol_version")]
    public int ProtocolVersion { get; set; } = Constants.NetworkProtocolVersion;

    [JsonPropertyName("apk_url")]
    public string ApkUrl { get; set; } = "";

    [JsonPropertyName("config")]
    public Dictionary<string, JsonElement> Config { get; set; } = new();

    [JsonPropertyName("packs")]
    public List<ResourcePackManifest> Packs { get; set; } = new();
}

public sealed class ResourcePackManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

public static class UpdatePolicy
{
    public static int CompareVersions(string left, string right)
    {
        var leftParts = ParseVersion(left);
        var rightParts = ParseVersion(right);
        for (var index = 0; index < Math.Max(leftParts.Length, rightParts.Length); index++)
        {
            var leftValue = index < leftParts.Length ? leftParts[index] : 0;
            var rightValue = index < rightParts.Length ? rightParts[index] : 0;
            if (leftValue != rightValue)
            {
                return leftValue.CompareTo(rightValue);
            }
        }

        return 0;
    }

    public static bool IsPackIdSafe(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
        {
            return false;
        }

        foreach (var character in id)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')
            {
                return false;
            }
        }

        return true;
    }

    public static string ComputeSha256(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private static int[] ParseVersion(string value)
    {
        var core = (value ?? "").Trim().Split('-', '+')[0];
        var segments = core.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var result = new int[Math.Max(segments.Length, 1)];
        for (var index = 0; index < segments.Length; index++)
        {
            result[index] = int.TryParse(segments[index], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                ? number
                : 0;
        }

        return result;
    }
}
