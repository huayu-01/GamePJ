using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

public class PlayerInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "Player";
    public int SeatIndex { get; set; }
}

public partial class NetworkManager : Node
{
    public static NetworkManager? Instance { get; private set; }

    [Signal] public delegate void PlayerConnectedEventHandler(int id, string name);
    [Signal] public delegate void PlayerDisconnectedEventHandler(int id);
    [Signal] public delegate void RoomCreatedEventHandler(string roomCode);
    [Signal] public delegate void JoinFailedEventHandler(string reason);
    [Signal] public delegate void GameStartedEventHandler();
    [Signal] public delegate void GameStateReceivedEventHandler(Godot.Collections.Dictionary state);

    [Export] public bool IsHost { get; set; }
    [Export] public new bool IsConnected { get; set; }
    [Export] public string RoomCode { get; set; } = "";
    [Export] public int LocalPlayerId { get; set; } = 1;
    [Export] public int RoomMaxPlayers { get; set; } = 9;

    public Dictionary<int, PlayerInfo> Players { get; } = new();
    private bool hostSignalsConnected;
    private bool clientSignalsConnected;

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        DisconnectSignals();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void CreateRoom(int port = Constants.DefaultPort, int maxPlayers = 9)
    {
        DisconnectSignals();
        RoomMaxPlayers = Mathf.Clamp(maxPlayers, 2, Constants.MaxPlayers);
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(port, RoomMaxPlayers);
        if (error != Error.Ok)
        {
            EmitSignal(SignalName.JoinFailed, $"CreateServer failed: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        hostSignalsConnected = true;

        IsHost = true;
        IsConnected = true;
        LocalPlayerId = 1;
        RoomCode = GenerateRoomCode();
        Players.Clear();
        Players[1] = new PlayerInfo { Id = 1, Name = PlayerData.Instance?.PlayerName ?? "Host", SeatIndex = 0 };

        EmitSignal(SignalName.RoomCreated, RoomCode);
        EmitSignal(SignalName.PlayerConnected, 1, Players[1].Name);
        Logger.Info($"Room created: {RoomCode}, {GetLocalIP()}:{port}");
    }

    public void JoinRoom(string address, int port = Constants.DefaultPort)
    {
        DisconnectSignals();
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(address, port);
        if (error != Error.Ok)
        {
            EmitSignal(SignalName.JoinFailed, $"CreateClient failed: {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
        clientSignalsConnected = true;
        IsHost = false;
        IsConnected = false;
    }

    public string GetLocalIP()
    {
        var fallback = "127.0.0.1";
        var candidates = new List<string>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    candidates.Add(address.Address.ToString());
                }
            }
        }

        return candidates.FirstOrDefault(ip => ip.StartsWith("192.168.") || ip.StartsWith("10.")) ??
               candidates.FirstOrDefault() ??
               fallback;
    }

    public string GenerateRoomCode()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        return rng.RandiRange(100000, 999999).ToString();
    }

    public string GetRoomPayload(int port = Constants.DefaultPort)
    {
        var payload = new Godot.Collections.Dictionary
        {
            ["v"] = 1,
            ["ip"] = GetLocalIP(),
            ["port"] = port,
            ["room"] = RoomCode,
            ["max_players"] = RoomMaxPlayers,
            ["small_blind"] = GameManager.Instance?.SmallBlindAmount ?? Constants.SmallBlind,
            ["big_blind"] = GameManager.Instance?.BigBlindAmount ?? Constants.BigBlind,
            ["min_buy_in"] = GameManager.Instance?.MinBuyIn ?? Constants.MinBuyIn,
            ["max_buy_in"] = GameManager.Instance?.MaxBuyIn ?? Constants.MaxBuyIn,
            ["table_chip_limit"] = GameManager.Instance?.TableChipLimit ?? Constants.TableChipLimit
        };

        return Json.Stringify(payload);
    }

    public void SubmitLocalAction(int playerId, PlayerAction action, int amount)
    {
        if (IsHost)
        {
            GameManager.Instance?.ProcessRemoteAction(playerId, (int)action, amount);
            return;
        }

        RpcId(1, MethodName.SubmitPlayerAction, playerId, (int)action, amount);
    }

    public void BroadcastGameState(Godot.Collections.Dictionary state)
    {
        if (!IsHost)
        {
            return;
        }

        Rpc(MethodName.SyncGameState, state);
    }

    public void BroadcastAction(int playerId, PlayerAction action, int amount)
    {
        if (!IsHost)
        {
            return;
        }

        Rpc(MethodName.SyncPlayerAction, playerId, (int)action, amount);
    }

    public void StartNetworkGame()
    {
        if (!IsHost)
        {
            return;
        }

        Rpc(MethodName.NotifyGameStarted);
        EmitSignal(SignalName.GameStarted);
    }

    public void LeaveRoom()
    {
        DisconnectSignals();
        Multiplayer.MultiplayerPeer?.Close();
        Multiplayer.MultiplayerPeer = null;
        IsHost = false;
        IsConnected = false;
        RoomCode = "";
        Players.Clear();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SubmitPlayerAction(int playerId, int action, int amount)
    {
        if (!IsHost)
        {
            return;
        }

        GameManager.Instance?.ProcessRemoteAction(playerId, action, amount);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncGameState(Godot.Collections.Dictionary state)
    {
        EmitSignal(SignalName.GameStateReceived, state);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncPlayerAction(int playerId, int action, int amount)
    {
        Logger.Info($"Action synced: P{playerId} {(PlayerAction)action} {amount}");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyGameStarted()
    {
        EmitSignal(SignalName.GameStarted);
    }

    private void OnPeerConnected(long id)
    {
        var playerId = (int)id;
        Players[playerId] = new PlayerInfo { Id = playerId, Name = $"玩家{playerId}", SeatIndex = FindFirstFreeSeat() };
        GameManager.Instance?.SyncPlayersFromNetwork();
        EmitSignal(SignalName.PlayerConnected, playerId, Players[playerId].Name);
    }

    private void OnPeerDisconnected(long id)
    {
        var playerId = (int)id;
        Players.Remove(playerId);
        GameManager.Instance?.SyncPlayersFromNetwork();
        EmitSignal(SignalName.PlayerDisconnected, playerId);
    }

    private void OnConnectedToServer()
    {
        IsConnected = true;
        LocalPlayerId = (int)Multiplayer.GetUniqueId();
        Players[LocalPlayerId] = new PlayerInfo { Id = LocalPlayerId, Name = PlayerData.Instance?.PlayerName ?? $"玩家{LocalPlayerId}", SeatIndex = 0 };
        Logger.Info("Connection successful");
    }

    private int FindFirstFreeSeat()
    {
        for (var seat = 0; seat < RoomMaxPlayers; seat++)
        {
            if (Players.Values.All(player => player.SeatIndex != seat))
            {
                return seat;
            }
        }

        return Mathf.Clamp(Players.Count, 0, RoomMaxPlayers - 1);
    }

    private void OnConnectionFailed()
    {
        IsConnected = false;
        EmitSignal(SignalName.JoinFailed, "Connection failed");
    }

    private void OnServerDisconnected()
    {
        IsConnected = false;
        EmitSignal(SignalName.JoinFailed, "Server disconnected");
    }

    private void DisconnectSignals()
    {
        if (hostSignalsConnected)
        {
            Multiplayer.PeerConnected -= OnPeerConnected;
            Multiplayer.PeerDisconnected -= OnPeerDisconnected;
            hostSignalsConnected = false;
        }

        if (clientSignalsConnected)
        {
            Multiplayer.ConnectedToServer -= OnConnectedToServer;
            Multiplayer.ConnectionFailed -= OnConnectionFailed;
            Multiplayer.ServerDisconnected -= OnServerDisconnected;
            clientSignalsConnected = false;
        }
    }
}
