using Godot;
using System.Collections.Generic;

public partial class VisualSettlementProbe : GameTable
{
    private bool probeConfigured;

    public override void _EnterTree()
    {
        ConfigureProbeGame();
    }

    public override async void _Ready()
    {
        ConfigureProbeGame();
        base._Ready();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        TriggerSettlement();

        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        SaveScreenshot("settlement_mid.png", "VISUAL_SETTLEMENT mid");

        await ToSignal(GetTree().CreateTimer(2.6), SceneTreeTimer.SignalName.Timeout);
        SaveScreenshot("settlement_done.png", "VISUAL_SETTLEMENT done");
        GetTree().Quit();
    }

    private void ConfigureProbeGame()
    {
        if (probeConfigured)
        {
            return;
        }

        var manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.AutoContinueHands = false;
        manager.WaitForSettlementAnimation = true;
        manager.TableSeatCount = 9;
        manager.Players.Clear();
        manager.AiPlayerIds.Clear();
        manager.Players.Add(new Player { Id = 1, Name = "玩家", Chips = 620, Position = 0 });
        manager.Players.Add(new Player { Id = 2, Name = "短码赢家", Chips = 0, Position = 1, IsAllIn = true });
        manager.Players.Add(new Player { Id = 3, Name = "边池赢家", Chips = 0, Position = 2, IsAllIn = true });
        manager.PotManager.CollectBets(new Dictionary<int, int> { [1] = 100, [2] = 500, [3] = 500 });

        if (PlayerData.Instance != null)
        {
            PlayerData.Instance.LocalPlayerId = 1;
        }

        probeConfigured = true;
    }

    private void TriggerSettlement()
    {
        var manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.LastWinners.Clear();
        manager.LastWinners.Add(2);
        manager.LastWinners.Add(3);
        manager.LastWinnings.Clear();
        manager.LastWinnings[2] = 300;
        manager.LastWinnings[3] = 800;
        manager.LastPotAwards.Clear();
        manager.LastPotAwards.AddRange(manager.PotManager.BuildPotAwards(new List<List<int>>
        {
            new() { 2 },
            new() { 3 }
        }));

        var winners = new Godot.Collections.Array<int> { 2, 3 };
        var winnings = new Godot.Collections.Dictionary { [2] = 300, [3] = 800 };
        manager.EmitSignal(GameManager.SignalName.GameEnded, winners, winnings);
    }

    private void SaveScreenshot(string fileName, string prefix)
    {
        var image = GetViewport().GetTexture().GetImage();
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://test_outputs"));
        var path = $"res://test_outputs/{fileName}";
        image.SavePng(path);
        GD.Print($"{prefix} screenshot={ProjectSettings.GlobalizePath(path)}");
    }
}
