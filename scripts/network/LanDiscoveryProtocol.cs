using System;
using System.Text;

public sealed class LanRoomInfo
{
    public string Address { get; init; } = "";
    public int Port { get; init; }
    public string RoomCode { get; init; } = "";
    public int PlayerCount { get; init; }
    public int MaxPlayers { get; init; }
    public int ProtocolVersion { get; init; }
    public string AppVersion { get; init; } = "";
}

public static class LanDiscoveryProtocol
{
    public const int DiscoveryPort = 17341;
    public const string Query = "GAMEPJ_DISCOVER_V2";
    private const string ResponsePrefix = "GAMEPJ_ROOM_V2";

    public static byte[] CreateQuery() => Encoding.UTF8.GetBytes(Query);

    public static byte[] CreateResponse(
        int gamePort,
        string roomCode,
        int playerCount,
        int maxPlayers,
        int protocolVersion = Constants.NetworkProtocolVersion,
        string appVersion = Constants.AppVersion)
    {
        return Encoding.UTF8.GetBytes(
            $"{ResponsePrefix}|{gamePort}|{roomCode}|{playerCount}|{maxPlayers}|{protocolVersion}|{appVersion}");
    }

    public static bool IsQuery(ReadOnlySpan<byte> data)
    {
        return Encoding.UTF8.GetString(data).Trim() == Query;
    }

    public static bool TryParseResponse(ReadOnlySpan<byte> data, string address, out LanRoomInfo room)
    {
        room = new LanRoomInfo();
        var parts = Encoding.UTF8.GetString(data).Trim().Split('|');
        if (parts.Length != 7 || parts[0] != ResponsePrefix ||
            !int.TryParse(parts[1], out var port) || port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(parts[2]) ||
            !int.TryParse(parts[3], out var playerCount) ||
            !int.TryParse(parts[4], out var maxPlayers) || maxPlayers < 2 ||
            !int.TryParse(parts[5], out var protocolVersion) || protocolVersion <= 0 ||
            string.IsNullOrWhiteSpace(parts[6]))
        {
            return false;
        }

        room = new LanRoomInfo
        {
            Address = address,
            Port = port,
            RoomCode = parts[2],
            PlayerCount = Math.Clamp(playerCount, 0, maxPlayers),
            MaxPlayers = maxPlayers,
            ProtocolVersion = protocolVersion,
            AppVersion = parts[6]
        };
        return true;
    }
}
