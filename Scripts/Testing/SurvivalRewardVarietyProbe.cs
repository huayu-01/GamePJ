using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Weapons;

namespace ActRoguelike.Testing;

public partial class SurvivalRewardVarietyProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "SurvivalRewardVariety";
	protected override Task RunChecks()
	{
		using var pool = new RewardPool(); using var build = new RunBuild();
		pool.Initialize(NodeSearch.FindInSubtree<Weapon>(Player)!, "chr_hunter", Director.ModeId);
		var roots = pool.Available(build).Where(r => r.RequiredModeIds.Length > 0).ToArray();
		Check(roots.Length == 4, "定位内容限制：十二专属项中开局四个入门件符合前置");
		var first = pool.RollMode(build, 3, null, true); var second = pool.RollMode(build, 3, null, true);
		Check(first.Count == 3 && second.Count == 3 && first.Concat(second).Select(r => r.Id).Distinct().Count() == 4, "连续两次专属候选覆盖全部合格入门件");
		var shown = roots.ToDictionary(r => r.Id, _ => 0);
		foreach (var row in first.Concat(second)) shown[row.Id]++;
		bool valid = true;
		for (int i = 0; i < 30; i++)
		{
			var offer = pool.RollMode(build, 3, null, true);
			valid &= offer.Count == 3 && offer.Select(r => r.Id).Distinct().Count() == 3 && offer.All(r => shown.ContainsKey(r.Id));
			foreach (var row in offer) shown[row.Id]++;
		}
		Check(valid && shown.Values.Max() - shown.Values.Min() <= 1, "多轮专属展示均衡且不重复、不泄漏未解锁关联件");
		Check(pool.RollMode(build, 3, null, false).All(r => r.RequiredModeIds.Length == 0), "普通升级仍使用普通奖励池");
		pool.Initialize(NodeSearch.FindInSubtree<Weapon>(Player)!, "chr_hunter", string.Empty);
		Check(pool.RollMode(build, 3, null, true).All(r => r.RequiredModeIds.Length == 0), "原冒险不出现死战专属，缺额正常回退");
		return Task.CompletedTask;
	}
}
