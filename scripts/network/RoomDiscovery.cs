using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class DiscoveredRoomInfo
{
    public string Address { get; init; } = "";
    public int Port { get; init; }
    public string RoomCode { get; init; } = "";
    public int PlayerCount { get; init; }
    public int MaxPlayers { get; init; }
    public int ProtocolVersion { get; init; }
    public string AppVersion { get; init; } = "";
}

public interface IRoomDirectoryProvider
{
    bool IsConfigured { get; }
    string UnavailableReason { get; }
    Task<IReadOnlyList<DiscoveredRoomInfo>> SearchAsync(CancellationToken cancellationToken);
}

public sealed class UnconfiguredRoomDirectoryProvider : IRoomDirectoryProvider
{
    public bool IsConfigured => false;
    public string UnavailableReason => "公网房间服务尚未接入，可暂时手动输入公网服务器地址。";

    public Task<IReadOnlyList<DiscoveredRoomInfo>> SearchAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DiscoveredRoomInfo> rooms = System.Array.Empty<DiscoveredRoomInfo>();
        return Task.FromResult(rooms);
    }
}
