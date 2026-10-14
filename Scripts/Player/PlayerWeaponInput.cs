using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Weapons;
using Godot;

namespace ActRoguelike.Player;

/// <summary>
/// 玩家武器输入：把 屏幕中心射线 / 鼠标按键 翻译成武器的瞄准点与开火请求。
/// 挂在武器节点下，因此天然拿到当前武器；敌人的武器由自己的 Behavior 驱动，不用这个脚本。
/// 预瞄的镜头 FOV 属于表现层，在这里处理，武器本身不碰相机。
/// </summary>
public partial class PlayerWeaponInput : Node3D
{
	[Export] public float MaxAimDistance { get; set; } = 200f;
	[Export] public float DefaultFov { get; set; } = 78f;
	[Export] public float AimLerpSpeed { get; set; } = 14f;

	private Weapon? _weapon;
	private Unit? _unit;
	private MeleeAttackAbility? _melee;

	public override void _Ready()
	{
		_weapon = GetParent<Weapon>();
	}

	public override void _Process(double delta)
	{
		if (_weapon == null)
		{
			return;
		}

		// 子节点先于父节点 _Ready，武器持有者要等武器自己准备好之后再取。
		_unit ??= _weapon.OwnerUnit;
		if (_unit == null || _unit.IsDead)
		{
			return;
		}

		// 近战角色（武器 Enabled=false）：左键改为触发近战重击，不走枪械逻辑。
		if (!_weapon.Enabled)
		{
			// 本轮新增：武器关了之后，左键**先看有没有"绑在左键上的技能"**（表里 InputAction 说了算），
			// 有就交给它 —— 能量掌（没有任何弹匣概念）走这条；没有才退到近战重击的老逻辑。
			if (Input.IsActionJustPressed("fire"))
			{
				Ability? bound = PlayerAbilityInput.FindByInputAction(_unit, "weapon_skill_1");
				if (bound != null)
				{
					// 方向不用在这里算：本组件的 `_Process` 末尾会把角色朝向持续对齐准星，
					// 而技能的 `Ability.Aim = Crosshair` 自己会解析屏幕中心射线（与枪械同一条路）。
					bound.TryActivate();
					return;
				}

				_melee ??= NodeSearch.FindInSubtree<MeleeAttackAbility>(_unit);
				if (_melee != null)
				{
					// 近战需要「范围内的目标」才会挥（能力自己会校验距离），够不着就不出招。
					Unit? victim = Targeting.FindNearestHostile(_unit, _unit, _melee.AttackRange);
					if (victim != null)
					{
						_melee.TryActivate(victim);
					}
				}
			}

			return;
		}

		Camera3D? camera = GetViewport().GetCamera3D();
		Vector3 direction;
		Vector3 aimPoint;

		if (camera != null)
		{
			Vector2 center = GetViewport().GetVisibleRect().Size * 0.5f;
			Vector3 origin = camera.ProjectRayOrigin(center);
			direction = camera.ProjectRayNormal(center);
			aimPoint = origin + direction * MaxAimDistance;

			var query = PhysicsRayQueryParameters3D.Create(origin, aimPoint, CombatLayers.World | CombatLayers.UnitMask);
			query.Exclude = new Godot.Collections.Array<Rid> { _unit.GetRid() };
			Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
			if (hit.Count > 0)
			{
				aimPoint = hit["position"].AsVector3();
			}
		}
		else
		{
			direction = -_unit.GlobalTransform.Basis.Z;
			aimPoint = _unit.GlobalPosition + direction * MaxAimDistance;
		}

		// 表驱动的技能（InputAction 绑在右键上的武技2）也能拿到准星：把方向写进角色朝向，
		// 技能的 `Ability.Aim = Crosshair` 就会顺着这个方向解析（AimUtil 走屏幕中心射线）。
		// ⚠️ 放在武器关闭分支之外：武技2 也可能出现在"武器还开着"的载具上（未来）。
		Ability? charge = PlayerAbilityInput.FindByInputAction(_unit, "weapon_skill_2");
		if (charge != null)
		{
			_unit.Movement?.SetFacing(direction);
		}

		bool aiming = Input.IsActionPressed("aim");
		_weapon.SetAiming(aiming);

		if (camera != null)
		{
			float target = aiming ? _weapon.AimFov : DefaultFov;
			camera.Fov = Mathf.Lerp(camera.Fov, target, (float)delta * AimLerpSpeed);
		}

		if (_unit.Movement != null)
		{
			// 预瞄时身体朝准心；否则交回“朝移动方向”。
			// 角色**持续**朝向准星（用户要求）：火力倾泻这类"指向准星"的技能，
			// 表现上角色与枪口也要跟着准星转，否则子弹从朝别处的枪口射出、看起来方向不对。
			// 滑铲用的是 Ability.AimSource.Facing，而 Facing 现在恒等于准星方向，两者不冲突。
			_unit.Movement.SetFacing(direction);
		}

		if (Input.IsActionJustPressed("reload"))
		{
			_weapon.TryReload();
		}

		bool wantsFire = _weapon.Automatic ? Input.IsActionPressed("fire") : Input.IsActionJustPressed("fire");
		if (wantsFire)
		{
			_weapon.TryFire(aimPoint);
		}
	}
}
