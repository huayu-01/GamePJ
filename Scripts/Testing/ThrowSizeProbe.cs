using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Rewards;
using ActRoguelike.Visual;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Testing;

public partial class ThrowSizeProbe : EliteTraitProbeSupport
{
	protected override string ProbeTag => "ThrowSize";
	protected override PlayerCharacterKind TestCharacter => PlayerCharacterKind.WeaponMaster;
	protected override WeaponPresetKind TestWeapon => WeaponPresetKind.Scythe;
	protected override async Task RunChecks()
	{
		foreach (var weapon in new[] { WeaponPresetKind.Scythe, WeaponPresetKind.Spear })
		{
			RunController.Instance!.EquipWeapon(weapon); await Wait(.12);
			var ability = (WeaponThrowAbility)Player.AbilitySlots.Resolve("weapon_skill_2")!;
			ability.HoldToCharge = false; ability.MaxThrowDistance = 3; ability.RecallDistance = 0;
			var hitbox = ability.GetNode<AttackHitbox>("Attack");
			var shape = hitbox.GetNode<CollisionShape3D>("Shape");
			var display = ability.GetNode<ThrownWeaponDisplay>("WeaponVisual");
			Vector3 original = hitbox.Scale, bodyScale = Player.Scale;
			Check(ability.TryPress(), "真实投掷起手：" + weapon);
			for (int i = 0; i < 150 && !ability.HasSettled; i++) await Wait(.02);
			Check(ability.HasSettled, "投掷实际到达停留位置：" + weapon);
			var size = ((BoxShape3D)shape.Shape!).Size;
			var target = new Unit { Faction = Faction.Enemy, MaxHealth = 5000 };
			target.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One * .02f } });
			Mode.AddChild(target); target.GlobalPosition = shape.GlobalPosition + shape.GlobalBasis.X * (size.X * .575f);
			await Wait(.04);
			Check(target.Health == target.MaxHealth, "窄目标处于原始判定之外：" + weapon);
			using var build = new RunBuild();
			var definition = RewardCatalog.Find("mut_cleave")!;
			build.Add(definition, new RewardContext { Player = Player, Weapon = NodeSearch.FindInSubtree<Weapon>(Player)!, Build = build });
			await Wait(.04);
			Check(Mathf.IsEqualApprox(ability.ThrowSizeScale, 1.3f) && hitbox.Scale.IsEqualApprox(original * 1.3f)
				&& Mathf.IsEqualApprox(display.GlobalBasis.Scale.X, 1.3f), "模型与伤害体积均扩大到1.3倍：" + weapon);
			int tick = ability.DamageTickCount;
			for (int i = 0; i < 100 && ability.DamageTickCount == tick; i++) await Wait(.02);
			hitbox.SweepBox(ability.WeaponPosition, ability.WeaponPosition);
			Check(target.Health < target.MaxHealth, "扩大的真实判定能命中原范围外目标：" + weapon);
			Check(Player.Scale == bodyScale, "角色模型和身体碰撞不随离手武器放大");
			RewardEffectHost.Ensure(Player).ReleaseReward(build, definition.Id); await Wait(.04);
			Check(ability.ThrowSizeScale == 1 && hitbox.Scale.IsEqualApprox(original) && Mathf.IsEqualApprox(display.GlobalBasis.Scale.X, 1), "来源撤销后模型和碰撞一起恢复：" + weapon);
			ability.ResetForLoadout(); target.QueueFree();
			Check(hitbox.Scale.IsEqualApprox(original) && !ability.InFlight, "取消／换装不残留放大：" + weapon);
		}
	}
}
