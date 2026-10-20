using ActRoguelike.Audio;
using ActRoguelike.Config;
using Godot;

namespace ActRoguelike.Combat;

public enum ImpactKind { None, Flesh, Armor, ArmorBreak, Shield, Block, Parry, Ward }

/// <summary>结算后唯一反馈路由。读取结果快照，不产生伤害、硬直或控制免疫。</summary>
public static class CombatFeedback
{
	private static readonly CombatFeedbackProfile Fallback = new();
	public static CombatFeedbackProfile Profile => ConfigIndex.Settings.CombatFeedback ?? Fallback;
	public static ImpactKind LastKind { get; private set; }
	public static int PresentedCount { get; private set; }
	public static int SoundCount { get; private set; }
	public static int CameraCount { get; private set; }
	private static ulong _soundAt, _cameraAt;
	private static ulong _soundFrame = ulong.MaxValue, _cameraFrame = ulong.MaxValue;
	private static ulong _effectFrame = ulong.MaxValue;
	private static int _effectCount;
	public static ImpactKind Classify(DamageResult result, HitInstance hit)
	{
		if (!result.Hit) return ImpactKind.None;
		if (hit.Parried) return ImpactKind.Parry;
		if (hit.Negated) return ImpactKind.Ward;
		if (result.Invulnerable) return ImpactKind.None;
		if (result.Blocked) return ImpactKind.Block;
		if (result.FinalDamage <= 0) return ImpactKind.None;
		if (result.ArmorBroken) return ImpactKind.ArmorBreak;
		if (result.ArmorLost > 0) return ImpactKind.Armor;
		if (result.ShieldLost > 0) return ImpactKind.Shield;
		return ImpactKind.Flesh;
	}
	public static Color ColorFor(ImpactKind kind) => kind switch
	{
		ImpactKind.Armor => new Color(.35f, .7f, 1),
		ImpactKind.ArmorBreak => new Color(.65f, .9f, 1),
		ImpactKind.Block or ImpactKind.Parry => new Color(1, .85f, .28f),
		ImpactKind.Ward or ImpactKind.Shield => new Color(.6f, .55f, 1),
		_ => new Color(1, .66f, .5f),
	};
	public static void Present(Unit target, HitInstance hit, DamageResult result)
	{
		ImpactKind kind = Classify(result, hit); LastKind = kind;
		var profile = Profile;
		// 周期伤害仍结算与跳字，但不打断视觉节奏；来源失效时也不冒领玩家反馈。
		if (!profile.Enabled || kind == ImpactKind.None || hit.Attack?.IsDamageOverTime == true) return;
		ulong now = Time.GetTicksMsec();
		bool priority = kind is ImpactKind.ArmorBreak or ImpactKind.Parry || result.Killed;
		if (now < target.FeedbackReadyAt && !priority) return;
		target.FeedbackReadyAt = now + (ulong)(Mathf.Max(0, profile.TargetCooldownSeconds) * 1000);
		PresentedCount++;
		float weight = Mathf.Clamp(hit.Attack?.FeedbackWeight ?? 1, .3f, 2);
		if (hit.Attack is { ComboStepCount: > 1 } combo && combo.ComboStep == combo.ComboStepCount - 1) weight *= profile.FinisherWeight;
		if (result.Critical) weight *= profile.CriticalWeight;
		if (kind == ImpactKind.ArmorBreak) weight *= profile.BreakWeight;
		if (result.Killed) weight *= profile.KillWeight;
		weight = Mathf.Clamp(weight, .3f, 2.5f);
		bool secondary = hit.Attack?.IsSecondary == true;
		if (secondary) weight *= .45f;
		target.GetNodeOrNull<UnitPresentation>("Presentation")?.ShowImpact(kind, profile.FlashSeconds);
		ulong frame = Engine.GetPhysicsFrames();
		if (_effectFrame != frame) { _effectFrame = frame; _effectCount = 0; }
		if (ConfigIndex.Settings.ShowHitEffects && _effectCount < Mathf.Max(0, profile.MaxEffectsPerFrame))
		{
			_effectCount++;
			if (kind == ImpactKind.Flesh) HitEffect.Spawn(target, hit.Position, Mathf.Sqrt(weight));
			else CombatImpactFx.Spawn(target, hit.Position, hit.Direction, kind, weight);
		}
		Unit? source = hit.Attacker is { } owner && GodotObject.IsInstanceValid(owner) ? owner : null;
		bool playerSource = source?.Faction == Faction.Player && !source.HasTag("Summon");
		bool playerInvolved = (target.Faction == Faction.Player && !target.HasTag("Summon")) || playerSource;
		bool contact = hit.ContactFeedback && playerSource && !secondary;
		if (contact && profile.LocalPauseEnabled)
		{
			float seconds = Mathf.Lerp(profile.LightPauseSeconds, profile.HeavyPauseSeconds, Mathf.Clamp(weight - 1, 0, 1));
			source!.ImpactPause?.Request(Mathf.Clamp(seconds, 0, profile.MaxPauseSeconds), profile.PauseCooldownSeconds);
		}
		if (!playerInvolved || secondary) return;
		if (now >= _soundAt && frame != _soundFrame)
		{
			_soundFrame = frame;
			_soundAt = now + (ulong)(Mathf.Max(0, profile.SoundCooldownSeconds) * 1000);
			string sound = kind switch { ImpactKind.Armor => profile.ArmorSound, ImpactKind.ArmorBreak => profile.BreakSound,
				ImpactKind.Block or ImpactKind.Parry => profile.BlockSound, _ => profile.FleshSound };
			float pitch = kind switch { ImpactKind.Armor => profile.ArmorPitch, ImpactKind.ArmorBreak => profile.BreakPitch,
				ImpactKind.Block or ImpactKind.Parry => profile.BlockPitch, _ => 1 / Mathf.Sqrt(weight) };
			if (AudioManager.PlayImpact(sound, pitch, result.Killed ? 1 : 0)) SoundCount++;
		}
		if (now >= _cameraAt && frame != _cameraFrame && profile.CameraStrength > 0 && target.GetViewport().GetCamera3D() is { } camera)
		{
			// 镜头必须看得到事件；远处穿透、召唤物或后台击杀不能拖动当前视角。
			if (camera.IsPositionBehind(hit.Position) || !camera.GetViewport().GetVisibleRect().HasPoint(camera.UnprojectPosition(hit.Position))) return;
			_cameraAt = now + (ulong)(Mathf.Max(0, profile.CameraCooldownSeconds) * 1000);
			_cameraFrame = frame;
			CameraImpact.Ensure(camera).Kick(hit.Direction, weight * (target.Faction == Faction.Player ? 1.3f : 1)); CameraCount++;
		}
	}
}
