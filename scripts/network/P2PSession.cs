using Godot;

public enum NetworkSessionMode
{
    EntertainmentP2P = 0,
    DedicatedServer = 1
}

public static class P2PSession
{
    public static string DisplayName(NetworkSessionMode mode)
    {
        return mode == NetworkSessionMode.EntertainmentP2P ? "娱乐模式 P2P" : "公网权威服务器";
    }

    public static string CreateInvitePayload(string roomCode, int port, int maxPlayers)
    {
        return Json.Stringify(new Godot.Collections.Dictionary
        {
            ["mode"] = "entertainment_p2p",
            ["room"] = roomCode,
            ["port"] = port,
            ["max_players"] = maxPlayers,
            ["protocol_version"] = Constants.NetworkProtocolVersion,
            ["app_version"] = Constants.AppVersion
        });
    }
}
