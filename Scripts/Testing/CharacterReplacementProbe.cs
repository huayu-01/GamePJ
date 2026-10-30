using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Run;
using ActRoguelike.Rewards;
using ActRoguelike.Visual;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>真实物理帧与动画事件驱动的换模验收，不调用模拟动画事件入口。</summary>
public partial class CharacterReplacementProbe : Node
{
	private int _pass;
	private int _fail;
	private Node _base = null!;
	private Unit _player = null!;
	private Unit _target = null!;
	private Camera3D _camera = null!;
	private readonly Dictionary<string, int> _hits = new();
	private readonly HashSet<string> _manualHits = new();
	public override async void _Ready()
	{
		try { await Run(); } catch (Exception error) { Check(false, error.ToString()); }
		GetTree().Paused = false;
		GD.Print($"[CharacterReplacement] 完成：通过 {_pass}，失败 {_fail}");
		ActRoguelike.Core.GameExit.Request(GetTree(), _fail == 0 ? 0 : 1);
	}
	private void Check(bool ok, string message)
	{
		if (ok) _pass++; else _fail++;
		GD.Print($"[CharacterReplacement] {(ok ? "PASS" : "FAIL")} {message}");
	}
	private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	private async Task<bool> Until(Func<bool> predicate, double seconds = 1.5)
	{
		ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
		while (!predicate() && Time.GetTicksMsec() < end) await Wait(0.01);
		return predicate();
	}
	private async Task Press(string action, double duration = 0.025)
	{
		Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
		await Wait(duration);
		Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
	}
	private async Task Shot(string name)
	{
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Check(GetViewport().GetTexture().GetImage().SavePng(ActRoguelike.Testing.ValidationArtifacts.Path($"res://Logs/evidence/{name}.png")) == Error.Ok, $"实拍 {name}");
	}
	private static BoneAttachment3D Grip(CharacterConfig row, CharacterDisplay display)
	{
		UnitVisualProfile profile = row.VisualProfile!;
		var template = profile.GripScene!.Instantiate<BoneAttachment3D>();
		string bone = string.IsNullOrEmpty(profile.GripBone) ? template.BoneName : profile.GripBone; template.Free();
		return profile.ResolveSkeleton(display.ActiveModel!)!.GetChildren().OfType<BoneAttachment3D>()
			.First(grip => grip.BoneName == bone && grip.GetNodeOrNull<Node3D>(profile.GripSocketPath) != null);
	}
	private static IEnumerable<string> RequiredAnimations(CharacterConfig row)
		=> row.Skills.Concat(ConfigIndex.Settings.Weapons.Where(w => w.OwnerCharacter?.Id == row.Id).SelectMany(w => w.Skills))
			.SelectMany(skill => skill.ComboSeries?.ComboAnimations.ToArray() ?? (string.IsNullOrEmpty(skill.AnimName) ? Array.Empty<string>() : new[] { skill.AnimName })).Distinct();
	private void Aim()
	{
		_player.GlobalPosition = _target.GlobalPosition + new Vector3(0, 0.05f, 2.1f);
		_player.Velocity = Vector3.Zero;
		_player.Rotation = Vector3.Zero;
		var rig = _player.GetNode<Node3D>("CameraRig");
		rig.SetPhysicsProcess(false);
		rig.SetProcessInput(false);
		_camera.GlobalPosition = _player.GlobalPosition + new Vector3(0, 2.2f, 4);
		_camera.LookAt(_target.GlobalPosition + Vector3.Up * 0.9f);
		_player.ResetPhysicsInterpolation();
		_camera.ResetPhysicsInterpolation();
	}
	private async Task Equip(PlayerCharacterKind character, WeaponPresetKind weapon)
	{
		RunSetup.Character = character;
		RunSetup.Weapon = weapon;
		await Wait(0.2);
		Aim();
		await Wait(0.15);
	}
	private async Task Run()
	{
		await Wait(0.3);
		RunSetup.Character = PlayerCharacterKind.Hunter;
		RunSetup.Weapon = WeaponPresetKind.EnergyPalm;
		RunSetup.HasSelection = true;
		_base = RuntimeResources.Load<PackedScene>("res://Scenes/Levels/Base.tscn").Instantiate();
		GetTree().Root.AddChild(_base);
		GetTree().CurrentScene = _base;
		await Wait(0.8);
		_player = _base.GetNode<Unit>("Player");
		_camera = new Camera3D { Fov = 48, Current = true };
		AddChild(_camera);
		_target = new Unit { Name = "CharacterProbeTarget", Faction = Faction.Enemy, MaxHealth = 10000, KnockbackResistance = 1 };
		_target.AddChild(new CollisionShape3D { Name = "Body", Position = Vector3.Up * 0.9f, Shape = new CapsuleShape3D { Height = 1.8f, Radius = 0.4f } });
		_base.AddChild(_target);
		_target.GlobalPosition = _player.GlobalPosition + new Vector3(0, 0, -2.1f);
		_target.HitReceived += hit => { string id = hit.Attack?.Ability ?? ""; _hits[id] = Count(id) + 1; if (hit.Attack is { IsManualWeaponAttack: true, CastId: > 0 }) _manualHits.Add(id); };
		await Wait(0.15);
		Check(_player.Scale.IsEqualApprox(Vector3.One) && _player.GetNode<CollisionShape3D>("Body").Shape is CapsuleShape3D { Height: 1.8f, Radius: 0.4f }, "玩家根尺度1，1.8米胶囊碰撞保留");
		Aim();
		var wall = new StaticBody3D { CollisionLayer = CombatLayers.World, CollisionMask = 0 };
		wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.3f, 4, 6) } });
		_base.AddChild(wall);
		wall.GlobalPosition = _player.GlobalPosition + new Vector3(2, 1, 0);
		await Wait(0.05);
		var collision = _player.MoveAndCollide(Vector3.Right * 4);
		Check(collision?.GetCollider() == wall, "新角色经真实物理移动被World墙体阻挡");
		wall.QueueFree();
		Aim();
		var enemyAttack = new AttackHitbox { Damage = 10, Knockback = 0, CollisionMask = _player.CollisionLayer, CollisionLayer = 0 };
		enemyAttack.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.7f } });
		_base.AddChild(enemyAttack);
		enemyAttack.GlobalPosition = _player.GlobalPosition + Vector3.Up * 0.9f;
		await Wait(0.05);
		float vitality = _player.Health + _player.Armor;
		enemyAttack.Activate(_target);
		await Wait(0.05);
		Check(_player.Health + _player.Armor < vitality, "敌方真实Area命中玩家身体胶囊并扣除生命/护甲");
		enemyAttack.Deactivate();
		enemyAttack.QueueFree();
		foreach (var pair in new[] {
			(PlayerCharacterKind.Hunter, WeaponPresetKind.EnergyPalm), (PlayerCharacterKind.Hunter, WeaponPresetKind.Crossbow), (PlayerCharacterKind.Hunter, WeaponPresetKind.Bow),
			(PlayerCharacterKind.WeaponMaster, WeaponPresetKind.SpiritSword), (PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Scythe), (PlayerCharacterKind.WeaponMaster, WeaponPresetKind.Spear) })
		{
			await Equip(pair.Item1, pair.Item2);
			var display = _player.GetNode<CharacterDisplay>("Appearance");
			CharacterConfig row = RunController.CharacterRow(pair.Item1)!; UnitVisualProfile profile = row.VisualProfile!;
			Check(display.CurrentModelPath == row.ModelPath && display.ActiveModel is { Visible: true }, $"{pair.Item2}实际可见模型来自当前档案={row.ModelPath}");
			Skeleton3D skeleton = profile.ResolveSkeleton(display.ActiveModel!)!;
			var grip = Grip(row, display);
			Check(skeleton.FindBone(grip.BoneName) >= 0 && grip.GetNode<Node3D>(profile.GripSocketPath).GetChildCount() > 0, $"{pair.Item2}武器挂在档案声明的实际骨骼与挂点");
			AnimationPlayer player = profile.ResolveAnimationPlayer(display.ActiveModel!)!;
			int valid = 0;
			string[] files = SkillAnimationLibrary.ListAnimationFiles(row.AnimLibraryDir);
			foreach (string file in files)
			{
				Animation animation = RuntimeResources.Load<Animation>(file);
				bool bound = player.HasAnimation(row.AnimLibraryName + "/" + animation.ResourceName) && SkillAnimationLibrary.ValidateBindings(animation, player, skeleton).Count == 0;
				if (bound) valid++;
			}
			Check(files.Length > 0 && valid == files.Length && RequiredAnimations(row).All(name => SkillAnimationLibrary.ResolveExact(player, row.AnimLibraryName, name).Length > 0), $"{pair.Item2}档案中的全部{files.Length}段动画绑定有效，当前角色及武器所需动画无缺失");
			if (pair.Item2 is WeaponPresetKind.Bow or WeaponPresetKind.SpiritSword) await Shot(pair.Item2 == WeaponPresetKind.Bow ? "characters_hunter_bow" : "characters_weapon_master_sword");
			if (pair.Item1 == PlayerCharacterKind.Hunter)
			{
				float priorHealth = _target.Health;
				await Press("weapon_skill_1", pair.Item2 == WeaponPresetKind.Bow ? 0.65 : 0.025);
				Check(await Until(() => _target.Health < priorHealth), $"{row.Id}/{pair.Item2}真实输入→动画事件→投射物命中掉血 {priorHealth}→{_target.Health}");
				Check(_manualHits.Contains(_player.AbilitySlots.Resolve("weapon_skill_1")!.AbilityId), $"{pair.Item2}真实攻击冻结主武技身份及出手序号");
			}
		}
		foreach (var test in new[] { (WeaponPresetKind.SpiritSword, "AbilityWeapon4Skill1", 5), (WeaponPresetKind.Scythe, "AbilityWeapon5Skill1", 3), (WeaponPresetKind.Spear, "AbilityWeapon6Skill1", 4) })
		{
			await Equip(PlayerCharacterKind.WeaponMaster, test.Item1);
			var combo = _player.GetNode<MeleeComboAbility>(test.Item2);
			var grip = Grip(RunController.CharacterRow(PlayerCharacterKind.WeaponMaster)!, _player.GetNode<CharacterDisplay>("Appearance"));
			Transform3D handBefore = _player.GlobalTransform.AffineInverse() * grip.GlobalTransform;
			string id = combo.AbilityId;
			int totalBefore = Count(id);
			await Press("weapon_skill_1");
			await Wait(0.05);
			Check(Count(id) == totalBefore, $"{test.Item1}前摇无伤害");
			for (int step = 0; step < test.Item3; step++)
			{
				int expectedHits = totalBefore + step + 1;
				Check(await Until(() => Count(id) >= expectedHits), $"{test.Item1}第{step + 1}段由真实动画事件命中，累计={Count(id) - totalBefore}");
				if (step == 0)
				{
					Transform3D handAfter = _player.GlobalTransform.AffineInverse() * grip.GlobalTransform;
					Check(handBefore.Origin.DistanceTo(handAfter.Origin) > .03f || handBefore.Basis.X.AngleTo(handAfter.Basis.X) > .08f || handBefore.Basis.Y.AngleTo(handAfter.Basis.Y) > .08f,
						$"{test.Item1}攻击时实际挂点相对角色运动，不依赖骨名、骨架单位或角色根移动");
					await Shot($"characters_combo_{test.Item1}");
				}
				await Wait(0.04);
				Check(Count(id) == expectedHits, $"{test.Item1}第{step + 1}段同目标不重复伤害");
				if (step + 1 < test.Item3) await Press("weapon_skill_1");
			}
			if (combo.ComboLoop)
			{
				int prior = Count(id);
				await Press("weapon_skill_1");
				Check(await Until(() => Count(id) > prior) && combo.ComboStep == 0, "长枪第4段之后真实回到第1段并再次命中");
			}
			int ended = Count(id);
			Check(_manualHits.Contains(id), $"{test.Item1}真实连段冻结主武技身份及出手序号");
			RunSetup.Weapon = test.Item1 == WeaponPresetKind.Spear ? WeaponPresetKind.SpiritSword : WeaponPresetKind.Spear;
			await Wait(0.35);
			Check(Count(id) == ended, $"{test.Item1}换装清理后伤害盒关闭");
		}
		// 基地展台与关卡使用同一配置；不写入正式装备记忆。
		foreach (string id in new[] { "chr_hunter", "chr_weapon_master" })
			Check(_base.GetNode<CharacterDisplay>($"CharacterRoster/{id}/InteractionComponent/Interaction/TypeModel/Visual").CurrentModelPath == ConfigRows.ById<CharacterConfig>(id)!.ModelPath, $"{id}基地展台同步读取当前档案");
		var level = RuntimeResources.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate();
		_base.QueueFree();
		await Wait(0.1);
		GetTree().Root.AddChild(level);
		GetTree().CurrentScene = level;
		await Wait(0.7);
		Check(level.GetNode<CharacterDisplay>("Player/Appearance").CurrentModelPath == RunController.CharacterRow(PlayerCharacterKind.WeaponMaster)!.ModelPath, "正式关卡同样使用当前角色档案模型");
	}
	private int Count(string id) => _hits.TryGetValue(id, out int count) ? count : 0;
}
