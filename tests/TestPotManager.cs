using Godot;
using System.Collections.Generic;

public partial class TestPotManager : Node
{
    public override void _Ready()
    {
        var (pass, fail) = Run();
        GD.Print("\n========== POT TEST RESULT ==========");
        GD.Print($"PASS: {pass}/5, FAIL: {fail}/5");
    }

    public static (int Pass, int Fail) Run()
    {
        int pass = 0, fail = 0;

        var pot1 = new PotManager();
        pot1.CollectBets(new Dictionary<int, int> { [1] = 100, [2] = 100, [3] = 100 });
        Check(pot1.MainPot == 300 && pot1.SidePots.Count == 0, "SimplePot", ref pass, ref fail);

        var pot2 = new PotManager();
        pot2.CollectBets(new Dictionary<int, int> { [1] = 100, [2] = 50, [3] = 30 });
        Check(
            pot2.MainPot == 90 &&
            pot2.SidePots.Count == 2 &&
            pot2.SidePots[0].Amount == 40 &&
            pot2.SidePots[0].EligiblePlayers.Count == 2 &&
            pot2.SidePots[1].Amount == 50 &&
            pot2.SidePots[1].EligiblePlayers.Count == 1,
            "SidePot",
            ref pass,
            ref fail);

        var pot3 = new PotManager();
        pot3.CollectBets(new Dictionary<int, int> { [1] = 100, [2] = 100 });
        var winnings = pot3.AwardPots(new List<List<int>> { new() { 1, 2 } });
        Check(winnings[1] == 100 && winnings[2] == 100, "SplitPot", ref pass, ref fail);

        var pot4 = new PotManager();
        pot4.CollectBets(new Dictionary<int, int> { [1] = 100, [2] = 500, [3] = 500 });
        var sideWinnings = pot4.AwardPots(new List<List<int>> { new() { 1 }, new() { 2 } });
        Check(
            pot4.MainPot == 300 &&
            pot4.SidePots.Count == 1 &&
            pot4.SidePots[0].Amount == 800 &&
            sideWinnings[1] == 300 &&
            sideWinnings[2] == 800,
            "AllInPlayerOnlyWinsEligiblePot",
            ref pass,
            ref fail);

        var awards = pot4.BuildPotAwards(new List<List<int>> { new() { 1 }, new() { 2 } });
        Check(
            awards.Count == 2 &&
            awards[0].PotIndex == 0 &&
            awards[0].Amount == 300 &&
            awards[0].Shares[1] == 300 &&
            awards[1].PotIndex == 1 &&
            awards[1].Amount == 800 &&
            awards[1].Shares[2] == 800,
            "PotAwardDetails",
            ref pass,
            ref fail);

        return (pass, fail);
    }

    private static void Check(bool condition, string name, ref int pass, ref int fail)
    {
        if (condition)
        {
            pass++;
            GD.Print($"PASS: {name}");
        }
        else
        {
            fail++;
            GD.Print($"FAIL: {name}");
        }
    }
}
