using System.Collections.Generic;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using Godot;

namespace ActRoguelike.Visual;

/// <summary>指定图案的实例释放闪光；复用真实能力事件，不推导按键或伪造命中。</summary>
public partial class UnitCastGlow : Node
{
    [Export] public string MeshName { get; set; } = string.Empty;
    [Export] public string[] MeshNames { get; set; } = System.Array.Empty<string>();
    [Export] public string FocusName { get; set; } = string.Empty;
    [Export] public Color GlowColor { get; set; } = new(.10f, 1f, .58f);
    [Export] public float Duration { get; set; } = .44f;
    [Export] public float RiseSeconds { get; set; } = .12f;
    [Export] public float PeakHoldSeconds { get; set; } = .025f;
    [Export] public float Energy { get; set; } = 5f;
    [Export] public Vector3 KickOffset { get; set; } = new(0,.025f,-.10f);
    public int PulseCount { get; private set; }
    public float Strength { get; private set; }
    public int TargetCount => _surfaces.Count;
    public string LastAbilityId { get; private set; } = string.Empty;
    private readonly HashSet<Ability> _abilities = new();
    private readonly List<(StandardMaterial3D Material, Color BaseColor)> _surfaces = new();
    private Unit? _unit;
    private Node3D _model = null!;
    private float _elapsed, _riseStart, _kickStrength;
    private Node3D? _focus;
    private Transform3D _focusRest;
    private bool _subscribed;

    public override void _Ready()
    {
        _model = GetParent<Node3D>();
        SetProcess(false);
        // 动态施法纹样由本组件独占材质；批量上色不得替换这些表面。
        foreach (string name in MeshNames.Length > 0 ? MeshNames : new[] { MeshName })
            if (_model.FindChild(name, true, false) is MeshInstance3D mesh)
                mesh.SetMeta("combat_dynamic_material", true);
        CallDeferred(nameof(Bind));
    }

    private void Bind()
    {
        for (Node? node = GetParent(); node != null; node = node.GetParent())
            if (node is Unit unit) { _unit = unit; break; }
        if (_unit == null) return;
        var names = MeshNames.Length > 0 ? MeshNames : new[] { MeshName };
        foreach (string name in new HashSet<string>(names))
        {
            if (_model.FindChild(name, true, false) is not MeshInstance3D { Mesh: { } geometry } mesh) continue;
            for (int i=0; i<geometry.GetSurfaceCount(); i++)
            {
                if (mesh.GetActiveMaterial(i) is not StandardMaterial3D source) continue;
                // 每块图案使用独立材质，不改变眼睛、布面或其他单位。
                var material = (StandardMaterial3D)source.Duplicate(true);
                material.EmissionEnabled = true;
                material.Emission = GlowColor; material.EmissionEnergyMultiplier = 0;
                mesh.SetSurfaceOverrideMaterial(i, material);
                _surfaces.Add((material, source.AlbedoColor));
            }
        }
        if (_surfaces.Count == 0) return;
        _focus = string.IsNullOrEmpty(FocusName) ? null : _model.FindChild(FocusName, true, false) as Node3D;
        if (_focus != null) _focusRest = _focus.Transform;
        foreach (Node child in _unit.GetChildren()) Hook(child);
        _unit.ChildEnteredTree += Hook; _unit.ChildExitingTree += Unhook;
        _unit.Died += OnDied;
        _subscribed = true;
    }

    private void Hook(Node node)
    {
        if (node is Ability ability && _abilities.Add(ability)) ability.PresentationTriggered += OnCue;
    }

    private void Unhook(Node node)
    {
        if (node is Ability ability && _abilities.Remove(ability)) ability.PresentationTriggered -= OnCue;
    }

    private void OnCue(Ability ability)
    {
        if (_surfaces.Count == 0 || _unit == null || _unit.IsDead || !_model.IsVisibleInTree()
            || !_unit.AbilitySlots.Owns(ability) || !ability.Enabled) return;
        PulseCount++; LastAbilityId = ability.AbilityId; _elapsed = 0;
        // 连续出手从当前亮度继续缓入；空闲出手则从暗处开始，避免瞬间跳到峰值。
        _riseStart = Strength; _kickStrength = 1;
        ApplyStrength(); SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_unit?.ImpactPause?.IsHolding == true) return;
        if (!_model.IsVisibleInTree() || _unit is null or { IsDead: true }) { Reset(); return; }
        // 视觉脉冲至少跨过若干渲染帧；首次材质／弹体编译的长帧不能吞掉整个渐亮段。
        _elapsed += Mathf.Min((float)delta, .05f);
        float duration = Mathf.Max(.02f, Duration);
        float rise = Mathf.Clamp(RiseSeconds, .01f, duration*.8f);
        float holdEnd = Mathf.Min(duration-.005f, rise+Mathf.Max(0, PeakHoldSeconds));
        Strength = _elapsed < rise ? Mathf.Lerp(_riseStart, 1, Ease(_elapsed/rise))
            : _elapsed <= holdEnd ? 1 : 1-Ease((_elapsed-holdEnd)/(duration-holdEnd));
        float remaining = Mathf.Clamp(1-_elapsed/duration, 0, 1);
        _kickStrength = remaining*remaining;
        ApplyStrength();
        if (_elapsed >= duration) SetProcess(false);
    }

    private static float Ease(float value)
    {
        float t = Mathf.Clamp(value, 0, 1);
        return t*t*(3-2*t);
    }

    private void ApplyStrength()
    {
        foreach (var surface in _surfaces)
        {
            surface.Material.EmissionEnergyMultiplier = Energy*Strength;
            surface.Material.AlbedoColor = surface.BaseColor.Lerp(new Color(.5f, 1f, .76f), Strength*.8f);
        }
        if (IsInstanceValid(_focus))
        {
            _focus!.Transform = _focusRest;
            _focus.Position += KickOffset*_kickStrength;
            _focus.RotateX(-.12f*_kickStrength);
        }
    }

    public void Reset() { _elapsed = _riseStart = _kickStrength = 0; Strength = 0; ApplyStrength(); SetProcess(false); }
    private void OnDied(HitInstance _) => Reset();

    public override void _ExitTree()
    {
        Reset();
        foreach (Ability ability in _abilities)
            if (IsInstanceValid(ability)) ability.PresentationTriggered -= OnCue;
        _abilities.Clear();
        if (_subscribed && IsInstanceValid(_unit))
        {
            _unit!.ChildEnteredTree -= Hook; _unit.ChildExitingTree -= Unhook; _unit.Died -= OnDied;
        }
        _subscribed = false;
        _surfaces.Clear();
    }
}
