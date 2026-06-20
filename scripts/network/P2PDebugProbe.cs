using Godot;
using System;
using System.IO;
using System.Linq;

public static class P2PDebugProbe
{
    public static bool TryStart(Node owner)
    {
        if (!OS.IsDebugBuild() || NetworkManager.Instance == null)
        {
            return false;
        }

        var arguments = OS.GetCmdlineUserArgs();
        var role = ReadArgument(arguments, "--p2p-probe");
        if (role is not ("host" or "client"))
        {
            return false;
        }

        var output = ReadArgument(arguments, "--p2p-probe-output");
        var portText = ReadArgument(arguments, "--p2p-probe-port");
        var expectedText = ReadArgument(arguments, "--p2p-probe-players");
        var runGame = ReadArgument(arguments, "--p2p-probe-game") == "1";
        var port = int.TryParse(portText, out var parsedPort) ? parsedPort : Constants.DefaultPort;
        var expectedPlayers = int.TryParse(expectedText, out var parsedExpected) ? parsedExpected : 3;
        output = string.IsNullOrWhiteSpace(output)
            ? ProjectSettings.GlobalizePath($"user://p2p_probe_{role}.txt")
            : output;

        var network = NetworkManager.Instance;
        var completed = false;
        void Finish(bool success, string message)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            var directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(output, $"{(success ? "PASS" : "FAIL")} {message}");
            owner.GetTree().CreateTimer(0.35).Timeout += () => owner.GetTree().Quit(success ? 0 : 1);
        }

        owner.GetTree().CreateTimer(runGame ? 30 : 15).Timeout += () =>
            Finish(false, $"{role} timed out; players={network.Players.Count}");

        var manager = GameManager.Instance;
        var actionCount = 0;
        var chatCount = 0;
        var gameStartScheduled = false;
        var lastActionKey = "";
        network.ChatMessageReceived += (_, _) => chatCount++;
        void TryAutoAction()
        {
            if (!runGame || completed || manager?.CurrentBettingRound == null)
            {
                return;
            }

            var localPlayerId = network.LocalPlayerId;
            var round = manager.CurrentBettingRound;
            if (round.GetCurrentPlayerId() != localPlayerId)
            {
                return;
            }

            var player = manager.Players.FirstOrDefault(item => item.Id == localPlayerId);
            if (player == null)
            {
                return;
            }

            var ownBet = round.PlayerBets.GetValueOrDefault(localPlayerId, 0);
            var callAmount = Math.Max(0, round.CurrentBet - ownBet);
            var action = round.IsValidAction(localPlayerId, PlayerAction.Check, 0)
                ? PlayerAction.Check
                : round.IsValidAction(localPlayerId, PlayerAction.Call, callAmount)
                    ? PlayerAction.Call
                    : PlayerAction.Fold;
            var amount = action == PlayerAction.Call ? callAmount : 0;
            var handNumber = manager.CreateStateDTO().HandNumber;
            var actionKey = $"{handNumber}:{manager.CurrentState}:{localPlayerId}:{round.CurrentBet}:{ownBet}:{player.Chips}";
            if (actionKey == lastActionKey)
            {
                return;
            }

            lastActionKey = actionKey;
            owner.GetTree().CreateTimer(0.05).Timeout += () =>
            {
                if (!completed && manager.CurrentBettingRound?.GetCurrentPlayerId() == localPlayerId)
                {
                    actionCount++;
                    network.SubmitLocalAction(localPlayerId, action, amount);
                }
            };
        }

        if (runGame && manager != null)
        {
            manager.GameEnded += (winners, _) =>
            {
                var winnerText = string.Join(',', winners);
                owner.GetTree().CreateTimer(0.5).Timeout += () =>
                {
                    var historyCount = manager.HandHistory.Count;
                    var requiredChatCount = role == "host" ? expectedPlayers : 1;
                    var synchronized = historyCount > 0 && chatCount >= requiredChatCount;
                    Finish(
                        synchronized,
                        $"{role} completed hand {manager.CreateStateDTO().HandNumber}; actions={actionCount}; winners={winnerText}; history={historyCount}; chat={chatCount}");
                };
            };
            manager.PlayerActionRequired += (_, _) => TryAutoAction();
            network.GameStateReceived += _ => TryAutoAction();
        }

        if (role == "host")
        {
            network.PlayerConnected += (_, _) =>
            {
                if (network.Players.Count >= expectedPlayers)
                {
                    if (!runGame)
                    {
                        Finish(true, $"host accepted {network.Players.Count} players on port {port}");
                        return;
                    }

                    if (gameStartScheduled)
                    {
                        return;
                    }

                    gameStartScheduled = true;
                    owner.GetTree().CreateTimer(0.5).Timeout += () =>
                    {
                        manager?.SyncPlayersFromNetwork();
                        manager?.ConfigureRoomRules(1, 50, 200, 1000, 0);
                        network.SendChatMessage(network.LocalPlayerId, $"probe-host-{network.LocalPlayerId}");
                        manager?.StartGame();
                        network.StartNetworkGame();
                    };
                }
            };
            network.JoinFailed += reason => Finish(false, $"host create failed: {reason}");
            network.CreateEntertainmentRoom(port, Mathf.Clamp(expectedPlayers, 2, Constants.MaxPlayers));
        }
        else
        {
            network.JoinSucceeded += () =>
            {
                network.SendChatMessage(network.LocalPlayerId, $"probe-client-{network.LocalPlayerId}");
                if (!runGame)
                {
                    Finish(true, $"client connected as peer {network.LocalPlayerId}");
                }
            };
            network.JoinFailed += reason => Finish(false, $"client failed: {reason}");
            network.JoinRoom("127.0.0.1", port);
        }

        return true;
    }

    private static string ReadArgument(string[] arguments, string name)
    {
        var prefix = name + "=";
        return arguments.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?
            .Substring(prefix.Length) ?? "";
    }
}
