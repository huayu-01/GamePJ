using Godot;

public partial class TestAll : Node
{
    public override void _Ready()
    {
        var hand = TestHandEvaluator.Run();
        var betting = TestBettingRound.Run();
        var pot = TestPotManager.Run();
        var flow = TestGameManagerFlow.Run();
        var totalPass = hand.Pass + betting.Pass + pot.Pass + flow.Pass;
        var totalFail = hand.Fail + betting.Fail + pot.Fail + flow.Fail;

        GD.Print("\n========== ALL TESTS ==========");
        GD.Print($"PASS: {totalPass}/67, FAIL: {totalFail}/67");
        GD.Print(totalFail == 0 ? "ALL LOGIC TESTS PASSED!" : "LOGIC TESTS FAILED");
        GetTree().Quit(totalFail == 0 ? 0 : 1);
    }
}
