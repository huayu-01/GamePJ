using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public partial class UpdateManager : Node
{
    public static UpdateManager? Instance { get; private set; }

    [Signal] public delegate void StatusChangedEventHandler(string status, bool danger);
    [Signal] public delegate void AppUpdateAvailableEventHandler(string version, string apkUrl, bool required);

    private const string UpdateRoot = "user://updates";
    private const string PackRoot = "user://updates/packs";
    private const string CachedManifestPath = "user://updates/manifest-cache.json";
    private const string RemoteConfigPath = "user://updates/remote-config.json";
    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private readonly Dictionary<string, JsonElement> _remoteConfig = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _checkCancellation;

    public string StatusText { get; private set; } = $"版本 {Constants.AppVersion}";
    public bool StatusIsDanger { get; private set; }
    public bool RestartRequired { get; private set; }
    public UpdateManifest? LastManifest { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
        EnsureDirectories();
        LoadCachedRemoteConfig();
        ActivateInstalledPacks();
    }

    public override void _Ready()
    {
        var manifestUrl = GetManifestUrl();
        if (!string.IsNullOrWhiteSpace(manifestUrl))
        {
            _checkCancellation = new CancellationTokenSource();
            _ = CheckForUpdatesAsync(manifestUrl, _checkCancellation);
        }
    }

    public override void _ExitTree()
    {
        _checkCancellation?.Cancel();
        _checkCancellation?.Dispose();
        _checkCancellation = null;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public string GetRemoteString(string key, string fallback = "")
    {
        return _remoteConfig.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    public int GetRemoteInt(string key, int fallback)
    {
        return _remoteConfig.TryGetValue(key, out var value) && value.TryGetInt32(out var result) ? result : fallback;
    }

    public bool GetRemoteBool(string key, bool fallback)
    {
        return _remoteConfig.TryGetValue(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
    }

    public void CheckNow()
    {
        var manifestUrl = GetManifestUrl();
        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            PublishStatus("未配置更新服务器，当前使用内置版本", false);
            return;
        }

        _checkCancellation?.Cancel();
        _checkCancellation?.Dispose();
        _checkCancellation = new CancellationTokenSource();
        _ = CheckForUpdatesAsync(manifestUrl, _checkCancellation);
    }

    public void PublishStatus(string status, bool danger)
    {
        StatusText = status;
        StatusIsDanger = danger;
        EmitSignal(SignalName.StatusChanged, status, danger);
    }

    public void PublishAppUpdate(string version, string apkUrl, bool required)
    {
        EmitSignal(SignalName.AppUpdateAvailable, version, apkUrl, required);
    }

    private async Task CheckForUpdatesAsync(string manifestUrl, CancellationTokenSource owner)
    {
        QueueStatus("正在检查资源更新...", false);
        try
        {
            ValidateRemoteUri(manifestUrl);
            var json = await Http.GetStringAsync(manifestUrl, owner.Token);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions)
                ?? throw new InvalidDataException("更新清单为空");
            ValidateManifest(manifest);
            LastManifest = manifest;
            WriteAtomic(CachedManifestPath, json);
            ApplyRemoteConfig(manifest.Config);

            var required = UpdatePolicy.CompareVersions(Constants.AppVersion, manifest.MinimumAppVersion) < 0 ||
                           manifest.ProtocolVersion != Constants.NetworkProtocolVersion;
            var available = UpdatePolicy.CompareVersions(Constants.AppVersion, manifest.LatestAppVersion) < 0;
            if (available || required)
            {
                CallDeferred(nameof(PublishAppUpdate), manifest.LatestAppVersion, manifest.ApkUrl, required);
            }

            var changedPacks = 0;
            foreach (var pack in manifest.Packs.Where(item => item.Enabled))
            {
                owner.Token.ThrowIfCancellationRequested();
                if (await InstallPackIfNeededAsync(pack, owner.Token))
                {
                    changedPacks++;
                }
            }

            if (required)
            {
                QueueStatus($"需要更新客户端至 {manifest.LatestAppVersion}", true);
            }
            else if (available)
            {
                QueueStatus($"发现客户端新版本 {manifest.LatestAppVersion}", false);
            }
            else if (changedPacks > 0)
            {
                RestartRequired = true;
                QueueStatus($"已更新 {changedPacks} 个资源包，重启后生效", false);
            }
            else
            {
                QueueStatus($"已是最新版本 {Constants.AppVersion}", false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.Warn($"Update check failed: {exception.Message}");
            QueueStatus("更新检查失败，已继续使用本地版本", false);
        }
        finally
        {
            if (ReferenceEquals(_checkCancellation, owner))
            {
                owner.Dispose();
                _checkCancellation = null;
            }
        }
    }

    private async Task<bool> InstallPackIfNeededAsync(ResourcePackManifest pack, CancellationToken cancellationToken)
    {
        if (!UpdatePolicy.IsPackIdSafe(pack.Id) || string.IsNullOrWhiteSpace(pack.Sha256) || pack.Sha256.Length != 64)
        {
            throw new InvalidDataException($"资源包定义无效: {pack.Id}");
        }

        ValidateRemoteUri(pack.Url);
        var activePath = Globalize($"{PackRoot}/{pack.Id}.pck");
        var downloadPath = activePath + ".download";
        var previousPath = activePath + ".previous";
        if (File.Exists(activePath) && string.Equals(await ComputeFileSha256Async(activePath, cancellationToken), pack.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        QueueStatus($"正在下载资源包 {pack.Id}...", false);
        using var response = await Http.GetAsync(pack.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = new FileStream(downloadPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.None, 81920, true))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        var info = new FileInfo(downloadPath);
        if (pack.Size > 0 && info.Length != pack.Size)
        {
            File.Delete(downloadPath);
            throw new InvalidDataException($"资源包大小不匹配: {pack.Id}");
        }

        var actualHash = await ComputeFileSha256Async(downloadPath, cancellationToken);
        if (!string.Equals(actualHash, pack.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(downloadPath);
            throw new InvalidDataException($"资源包校验失败: {pack.Id}");
        }

        if (File.Exists(previousPath))
        {
            File.Delete(previousPath);
        }
        if (File.Exists(activePath))
        {
            File.Move(activePath, previousPath);
        }
        File.Move(downloadPath, activePath);
        return true;
    }

    private void ActivateInstalledPacks()
    {
        var packDirectory = Globalize(PackRoot);
        foreach (var activePath in Directory.EnumerateFiles(packDirectory, "*.pck", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
        {
            if (ProjectSettings.LoadResourcePack(activePath, true))
            {
                continue;
            }

            Logger.Warn($"Resource pack failed to load, rolling back: {activePath}");
            var previousPath = activePath + ".previous";
            try
            {
                File.Delete(activePath);
                if (File.Exists(previousPath))
                {
                    File.Move(previousPath, activePath);
                    ProjectSettings.LoadResourcePack(activePath, true);
                }
            }
            catch (Exception exception)
            {
                Logger.Error($"Resource pack rollback failed: {exception.Message}");
            }
        }
    }

    private void ApplyRemoteConfig(Dictionary<string, JsonElement> config)
    {
        _remoteConfig.Clear();
        foreach (var pair in config)
        {
            _remoteConfig[pair.Key] = pair.Value.Clone();
        }
        WriteAtomic(RemoteConfigPath, JsonSerializer.Serialize(_remoteConfig, JsonOptions));
    }

    private void LoadCachedRemoteConfig()
    {
        var path = Globalize(RemoteConfigPath);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path), JsonOptions);
            if (config != null)
            {
                foreach (var pair in config)
                {
                    _remoteConfig[pair.Key] = pair.Value.Clone();
                }
            }
        }
        catch (Exception exception)
        {
            Logger.Warn($"Remote config cache ignored: {exception.Message}");
        }
    }

    private static async Task<string> ComputeFileSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read, 81920, true);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ValidateManifest(UpdateManifest manifest)
    {
        if (manifest.ManifestVersion != 1 || manifest.ProtocolVersion <= 0 || string.IsNullOrWhiteSpace(manifest.LatestAppVersion))
        {
            throw new InvalidDataException("不支持的更新清单");
        }
    }

    private static void ValidateRemoteUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(OS.IsDebugBuild() && uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)))
        {
            throw new InvalidDataException("更新地址必须使用 HTTPS");
        }
    }

    private static string GetManifestUrl()
    {
        var environment = OS.GetEnvironment("GAMEPJ_UPDATE_MANIFEST_URL").Trim();
        return !string.IsNullOrWhiteSpace(environment)
            ? environment
            : ProjectSettings.GetSetting("game/update/manifest_url", "").AsString().Trim();
    }

    private static void EnsureDirectories()
    {
        Directory.CreateDirectory(Globalize(UpdateRoot));
        Directory.CreateDirectory(Globalize(PackRoot));
        foreach (var partial in Directory.EnumerateFiles(Globalize(PackRoot), "*.download", SearchOption.TopDirectoryOnly))
        {
            File.Delete(partial);
        }
    }

    private static void WriteAtomic(string godotPath, string content)
    {
        var destination = Globalize(godotPath);
        var temporary = destination + ".tmp";
        File.WriteAllText(temporary, content);
        File.Move(temporary, destination, true);
    }

    private static string Globalize(string path) => ProjectSettings.GlobalizePath(path);

    private void QueueStatus(string status, bool danger)
    {
        CallDeferred(nameof(PublishStatus), status, danger);
    }
}
