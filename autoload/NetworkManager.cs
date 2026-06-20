using Godot;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
    [Signal] public delegate void JoinSucceededEventHandler();
    [Signal] public delegate void JoinFailedEventHandler(string reason);
    [Signal] public delegate void GameStartedEventHandler();
    [Signal] public delegate void GameStateReceivedEventHandler(Godot.Collections.Dictionary state);
    [Signal] public delegate void ChatMessageReceivedEventHandler(int playerId, string message);
    [Signal] public delegate void RoomDiscoveredEventHandler(string address, int port, string roomCode, int playerCount, int maxPlayers);
    [Signal] public delegate void RoomDiscoveryFinishedEventHandler(string message);

    [Export] public bool IsHost { get; set; }
    [Export] public new bool IsConnected { get; set; }
    [Export] public string RoomCode { get; set; } = "";
    [Export] public int LocalPlayerId { get; set; } = 1;
    [Export] public int RoomMaxPlayers { get; set; } = 9;
    [Export] public NetworkSessionMode SessionMode { get; set; } = NetworkSessionMode.EntertainmentP2P;
    [Export] public int SessionPort { get; set; } = Constants.DefaultPort;

    public Dictionary<int, PlayerInfo> Players { get; } = new();
    private bool hostSignalsConnected;
    private bool clientSignalsConnected;
    private readonly ConcurrentQueue<DiscoveredRoomInfo> _discoveredRooms = new();
    private IRoomDirectoryProvider _roomDirectoryProvider = new UnconfiguredRoomDirectoryProvider();
    private CancellationTokenSource? _roomDiscoveryCancellation;
    private int _discoveryFinishedPending;
    private string _discoveryFinishedMessage = "";

    public override void _Ready()
    {
        Instance = this;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        while (_discoveredRooms.TryDequeue(out var room))
        {
            EmitSignal(
                SignalName.RoomDiscovered,
                room.Address,
                room.Port,
                room.RoomCode,
                room.PlayerCount,
                room.MaxPlayers);
        }

        if (Interlocked.Exchange(ref _discoveryFinishedPending, 0) == 1)
        {
            EmitSignal(SignalName.RoomDiscoveryFinished, _discoveryFinishedMessage);
        }
    }

    public override void _ExitTree()
    {
        StopRoomDiscovery();
        DisconnectSignals();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void CreateRoom(int port = Constants.DefaultPort, int maxPlayers = 9)
    {
        CreateEntertainmentRoom(port, maxPlayers);
    }

    public void CreateEntertainmentRoom(int port = Constants.DefaultPort, int maxPlayers = 9)
    {
        StopRoomDiscovery();
        DisconnectSignals();
        SessionMode = NetworkSessionMode.EntertainmentP2P;
        SessionPort = port;
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
        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.LocalPlayerId = LocalPlayerId;
        }
        RoomCode = GenerateRoomCode();
        Players.Clear();
        Players[1] = new PlayerInfo { Id = 1, Name = PlayerData.Instance?.PlayerName ?? "Host", SeatIndex = 0 };
        EmitSignal(SignalName.RoomCreated, RoomCode);
        EmitSignal(SignalName.PlayerConnected, 1, Players[1].Name);
        Logger.Info($"Room created: {RoomCode}, port {port}");
    }

    public void JoinRoom(string address, int port = Constants.DefaultPort)
    {
        StopRoomDiscovery();
        DisconnectSignals();
        SessionMode = NetworkSessionMode.EntertainmentP2P;
        SessionPort = port;
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

    public string GetEntertainmentInvitePayload()
    {
        return P2PSession.CreateInvitePayload(RoomCode, SessionPort, RoomMaxPlayers);
    }

    public string GenerateRoomCode()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        return rng.RandiRange(100000, 999999).ToString();
    }

    public void ConfigureRoomDirectoryProvider(IRoomDirectoryProvider provider)
    {
        _roomDirectoryProvider = provider ?? new UnconfiguredRoomDirectoryProvider();
    }

    public void DiscoverRooms()
    {
        StopRoomDiscovery();
        while (_discoveredRooms.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref _discoveryFinishedPending, 0);
        _roomDiscoveryCancellation = new CancellationTokenSource();
        _ = DiscoverRoomsAsync(_roomDiscoveryCancellation.Token);
    }

    public void StopRoomDiscovery()
    {
        var cancellation = Interlocked.Exchange(ref _roomDiscoveryCancellation, null);
        if (cancellation == null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
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

    public void SendChatMessage(int playerId, string message)
    {
        var normalized = NormalizeChatMessage(message);
        if (playerId <= 0 || string.IsNullOrEmpty(normalized))
        {
            return;
        }

        if (Multiplayer.MultiplayerPeer == null || !IsConnected)
        {
            EmitSignal(SignalName.ChatMessageReceived, playerId, normalized);
            return;
        }

        if (IsHost)
        {
            BroadcastChatMessage(playerId, normalized);
            return;
        }

        RpcId(1, MethodName.SubmitChatMessage, playerId, normalized);
    }

    public void BroadcastHistoryEntry(string message)
    {
        if (!IsHost || !IsConnected || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Rpc(MethodName.SyncHistoryEntry, message);
    }

    public void RequestHoleCardReveal(int playerId, int cardIndex)
    {
        if (IsHost)
        {
            ProcessHoleCardReveal(playerId, cardIndex);
            return;
        }

        RpcId(1, MethodName.SubmitHoleCardReveal, playerId, cardIndex);
    }

    public void BroadcastHandResult(
        IEnumerable<Player> revealedPlayers,
        Godot.Collections.Array<int> winners,
        Godot.Collections.Dictionary winnings,
        IEnumerable<PotAward> awards,
        bool wentToShowdown)
    {
        if (!IsHost)
        {
            return;
        }

        var revealedHands = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var player in revealedPlayers)
        {
            var cards = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            foreach (var card in player.HoleCards)
            {
                if (card != null)
                {
                    cards.Add(CardDTO.FromCard(card).ToDictionary());
                }
            }
            revealedHands.Add(new Godot.Collections.Dictionary { ["player_id"] = player.Id, ["cards"] = cards });
        }

        var serializedAwards = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var award in awards)
        {
            var shares = new Godot.Collections.Dictionary();
            foreach (var pair in award.Shares)
            {
                shares[pair.Key] = pair.Value;
            }
            serializedAwards.Add(new Godot.Collections.Dictionary
            {
                ["pot_index"] = award.PotIndex,
                ["amount"] = award.Amount,
                ["eligible_players"] = award.EligiblePlayers.ToGodotArray(),
                ["winners"] = award.Winners.ToGodotArray(),
                ["shares"] = shares
            });
        }

        Rpc(MethodName.SyncHandResult, revealedHands, winners, winnings, serializedAwards, wentToShowdown);
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

    public void SendPrivateHoleCards(int playerId, Card?[] holeCards)
    {
        if (!IsHost)
        {
            return;
        }

        var cards = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var card in holeCards)
        {
            if (card != null)
            {
                cards.Add(CardDTO.FromCard(card).ToDictionary());
            }
        }

        if (playerId == 1)
        {
            GameManager.Instance?.ApplyPrivateHoleCards(playerId, cards);
            return;
        }

        RpcId(playerId, MethodName.SyncPrivateHoleCards, playerId, cards);
    }

    public void LeaveRoom()
    {
        StopRoomDiscovery();
        DisconnectSignals();
        Multiplayer.MultiplayerPeer?.Close();
        Multiplayer.MultiplayerPeer = null;
        IsHost = false;
        IsConnected = false;
        RoomCode = "";
        SessionMode = NetworkSessionMode.EntertainmentP2P;
        SessionPort = Constants.DefaultPort;
        Players.Clear();
        LocalPlayerId = 1;
        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.LocalPlayerId = LocalPlayerId;
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SubmitPlayerAction(int playerId, int action, int amount)
    {
        if (!IsHost)
        {
            return;
        }

        var senderId = (int)Multiplayer.GetRemoteSenderId();
        if (senderId > 1 && senderId != playerId)
        {
            Logger.Warn($"Rejected action for P{playerId} from peer {senderId}.");
            return;
        }

        GameManager.Instance?.ProcessRemoteAction(playerId, action, amount);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SubmitChatMessage(int playerId, string message)
    {
        if (!IsHost)
        {
            return;
        }

        var senderId = (int)Multiplayer.GetRemoteSenderId();
        if (senderId > 1 && senderId != playerId)
        {
            Logger.Warn($"Rejected chat for P{playerId} from peer {senderId}.");
            return;
        }

        var normalized = NormalizeChatMessage(message);
        if (!string.IsNullOrEmpty(normalized))
        {
            BroadcastChatMessage(playerId, normalized);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SubmitHoleCardReveal(int playerId, int cardIndex)
    {
        if (!IsHost)
        {
            return;
        }

        var senderId = (int)Multiplayer.GetRemoteSenderId();
        if (senderId > 1 && senderId != playerId)
        {
            Logger.Warn($"Rejected reveal for P{playerId} from peer {senderId}.");
            return;
        }

        ProcessHoleCardReveal(playerId, cardIndex);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncGameState(Godot.Collections.Dictionary state)
    {
        if (IsHost)
        {
            return;
        }

        GameManager.Instance?.ApplyNetworkState(state);
        EmitSignal(SignalName.GameStateReceived, state);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncPrivateHoleCards(int playerId, Godot.Collections.Array<Godot.Collections.Dictionary> cards)
    {
        GameManager.Instance?.ApplyPrivateHoleCards(playerId, cards);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncPublicHoleCard(int playerId, int cardIndex, Godot.Collections.Dictionary card)
    {
        GameManager.Instance?.ApplyPublicHoleCard(playerId, cardIndex, card);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncHandResult(
        Godot.Collections.Array<Godot.Collections.Dictionary> revealedHands,
        Godot.Collections.Array<int> winners,
        Godot.Collections.Dictionary winnings,
        Godot.Collections.Array<Godot.Collections.Dictionary> potAwards,
        bool wentToShowdown)
    {
        GameManager.Instance?.ApplyNetworkHandResult(revealedHands, winners, winnings, potAwards, wentToShowdown);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncPlayerAction(int playerId, int action, int amount)
    {
        Logger.Info($"Action synced: P{playerId} {(PlayerAction)action} {amount}");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncChatMessage(int playerId, string message)
    {
        EmitSignal(SignalName.ChatMessageReceived, playerId, message);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncHistoryEntry(string message)
    {
        GameManager.Instance?.ApplyNetworkHistoryEntry(message);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyGameStarted()
    {
        EmitSignal(SignalName.GameStarted);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SyncLobbyPlayers(Godot.Collections.Array<Godot.Collections.Dictionary> players, int roomMaxPlayers)
    {
        if (IsHost)
        {
            return;
        }

        RoomMaxPlayers = Mathf.Clamp(roomMaxPlayers, 2, Constants.MaxPlayers);
        Players.Clear();
        foreach (var player in players)
        {
            var id = player.GetValueOrDefault("id", 0).AsInt32();
            if (id <= 0)
            {
                continue;
            }

            Players[id] = new PlayerInfo
            {
                Id = id,
                Name = player.GetValueOrDefault("name", $"玩家{id}").AsString(),
                SeatIndex = player.GetValueOrDefault("seat_index", 0).AsInt32()
            };
        }

        GameManager.Instance?.SyncPlayersFromNetwork();
        EmitSignal(SignalName.PlayerConnected, LocalPlayerId, Players.GetValueOrDefault(LocalPlayerId)?.Name ?? "玩家");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RegisterPlayer(string playerName, int protocolVersion, string appVersion)
    {
        if (!IsHost)
        {
            return;
        }

        var playerId = (int)Multiplayer.GetRemoteSenderId();
        if (playerId <= 1)
        {
            return;
        }

        if (protocolVersion != Constants.NetworkProtocolVersion)
        {
            var reason = $"网络协议不兼容：房主为 {Constants.NetworkProtocolVersion}，你的版本为 {protocolVersion}。请更新客户端。";
            RpcId(playerId, MethodName.RejectIncompatibleClient, reason);
            GetTree().CreateTimer(0.35).Timeout += () => Multiplayer.MultiplayerPeer?.DisconnectPeer(playerId);
            Logger.Warn($"Rejected peer {playerId}: protocol {protocolVersion}, app {appVersion}");
            return;
        }

        if (!Players.TryGetValue(playerId, out var info))
        {
            info = new PlayerInfo { Id = playerId, SeatIndex = FindFirstFreeSeat() };
            Players[playerId] = info;
        }

        info.Name = string.IsNullOrWhiteSpace(playerName) ? $"玩家{playerId}" : playerName.Trim();
        GameManager.Instance?.SyncPlayersFromNetwork();
        EmitSignal(SignalName.PlayerConnected, playerId, info.Name);
        BroadcastLobbyPlayers();
        RpcId(playerId, MethodName.AcceptRegistration);
    }

    private void OnPeerConnected(long id)
    {
        Logger.Info($"Peer {id} connected, waiting for protocol registration.");
    }

    private void OnPeerDisconnected(long id)
    {
        var playerId = (int)id;
        GameManager.Instance?.HandlePlayerDisconnected(playerId);
        Players.Remove(playerId);
        GameManager.Instance?.SyncPlayersFromNetwork();
        EmitSignal(SignalName.PlayerDisconnected, playerId);
        BroadcastLobbyPlayers();
    }

    private void OnConnectedToServer()
    {
        IsConnected = false;
        LocalPlayerId = (int)Multiplayer.GetUniqueId();
        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.LocalPlayerId = LocalPlayerId;
        }
        Players[LocalPlayerId] = new PlayerInfo { Id = LocalPlayerId, Name = PlayerData.Instance?.PlayerName ?? $"玩家{LocalPlayerId}", SeatIndex = 0 };
        RpcId(
            1,
            MethodName.RegisterPlayer,
            PlayerData.Instance?.PlayerName ?? $"玩家{LocalPlayerId}",
            Constants.NetworkProtocolVersion,
            Constants.AppVersion);
        Logger.Info("Transport connected, waiting for protocol registration.");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void AcceptRegistration()
    {
        IsConnected = true;
        EmitSignal(SignalName.JoinSucceeded);
        Logger.Info("Connection and protocol registration successful.");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RejectIncompatibleClient(string reason)
    {
        EmitSignal(SignalName.JoinFailed, reason);
        CallDeferred(nameof(LeaveRoom));
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

    private void BroadcastChatMessage(int playerId, string message)
    {
        EmitSignal(SignalName.ChatMessageReceived, playerId, message);
        Rpc(MethodName.SyncChatMessage, playerId, message);
    }

    private static string NormalizeChatMessage(string message)
    {
        var normalized = (message ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 80 ? normalized : normalized[..80];
    }

    private void BroadcastLobbyPlayers()
    {
        if (!IsHost || !IsConnected)
        {
            return;
        }

        var roster = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var player in Players.Values.OrderBy(player => player.SeatIndex))
        {
            roster.Add(new Godot.Collections.Dictionary
            {
                ["id"] = player.Id,
                ["name"] = player.Name,
                ["seat_index"] = player.SeatIndex
            });
        }

        Rpc(MethodName.SyncLobbyPlayers, roster, RoomMaxPlayers);
    }

    private void ProcessHoleCardReveal(int playerId, int cardIndex)
    {
        var manager = GameManager.Instance;
        if (manager == null || manager.LastWinners.Count == 0 || !manager.RevealHoleCard(playerId, cardIndex))
        {
            return;
        }

        var card = manager.Players.FirstOrDefault(player => player.Id == playerId)?.HoleCards.ElementAtOrDefault(cardIndex);
        if (card != null)
        {
            Rpc(MethodName.SyncPublicHoleCard, playerId, cardIndex, CardDTO.FromCard(card).ToDictionary());
        }
    }

    private async Task DiscoverRoomsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rooms = await _roomDirectoryProvider.SearchAsync(cancellationToken);
            foreach (var room in rooms)
            {
                if (room.ProtocolVersion != Constants.NetworkProtocolVersion)
                {
                    Logger.Info($"Ignored incompatible room {room.RoomCode}: protocol {room.ProtocolVersion}.");
                    continue;
                }

                _discoveredRooms.Enqueue(room);
            }

            _discoveryFinishedMessage = _roomDirectoryProvider.IsConfigured
                ? "搜索完成"
                : _roomDirectoryProvider.UnavailableReason;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _discoveryFinishedMessage = $"房间目录连接失败：{exception.Message}";
            Logger.Warn(_discoveryFinishedMessage);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Interlocked.Exchange(ref _discoveryFinishedPending, 1);
            }
        }
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
