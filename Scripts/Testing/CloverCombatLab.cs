using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ActRoguelike.Abilities;
using ActRoguelike.Combat;
using ActRoguelike.Config;
using ActRoguelike.Core;
using ActRoguelike.Player;
using ActRoguelike.Run;
using ActRoguelike.Visual;
using ActRoguelike.World;
using Godot;

namespace ActRoguelike.Testing;

/// <summary>猎手真实装备与森林战斗的试装入口；仅本进程临时选择视觉档案。</summary>
public partial class CloverCombatLab : Node3D
{
    private readonly PlayerLoadout _loadout = new();
    private readonly List<Unit> _enemies = new();
    private readonly List<object> _checks = new();
    private readonly List<object> _glowSamples = new();
    private CharacterConfig _row = null!;
    private UnitVisualProfile? _originalProfile;
    private UnitVisualProfile _profile = null!;
    private PlayerController _player = null!;
    private Unit _target = null!;
    private CharacterDisplay _display = null!;
    private Camera3D _overview = null!;
    private Label _status = null!;
    private Vector3 _center;
    private bool _ready, _verify, _front, _enemyPaused;
    private int _pass, _fail;
    private WeaponPresetKind _weapon = WeaponPresetKind.EnergyPalm;
    private const string Output = "res://Logs/evidence/clover-combat/";
    private UnitAnimator Animator => _player.GetNode<UnitAnimator>(UnitAnimator.NodeName);
    private UnitCastGlow Glow => _display.ActiveModel!.GetNode<UnitCastGlow>("CastGlow");
    private CharacterMovement Movement => _player.Movement!;

    public override async void _Ready()
    {
        _verify = OS.GetCmdlineUserArgs().Contains("--clover-verify");
        try
        {
            // 配置索引在autoload的延迟初始化中建立，主场景不能抢在它前面查询行。
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            _profile = RuntimeResources.Load<UnitVisualProfile>("res://Config/VisualProfiles/clover_wayfarer.tres");
            _row = ConfigRows.ById<CharacterConfig>("chr_hunter")!;
            _originalProfile = _row.VisualProfile;
            // 只改这个独立场景进程的资源实例，不写角色表、默认场景或玩家存档。
            _row.VisualProfile = _profile;
            RunSetup.HasSelection = true; RunSetup.Character = PlayerCharacterKind.Hunter; RunSetup.Weapon = _weapon;
            AddChild(RuntimeResources.Load<PackedScene>("res://Scenes/Levels/ExpeditionForest.tscn").Instantiate());
            AddChild(new MapManager { Name = "MapManager" });
            _center = GetNode<Marker3D>("ExpeditionForest/NatureArena/CombatMarkers/PlayerStart").GlobalPosition;
            _player = RuntimeResources.Load<PackedScene>("res://Scenes/Player/Player.tscn").Instantiate<PlayerController>();
            AddChild(_player);
            _player.GetNode<UnitPresentation>("Presentation").RemoveOnDeath=false;
            _display = _player.GetNode<CharacterDisplay>("Appearance");
            _loadout.Apply(_player, this, PlayerCharacterKind.Hunter, _weapon);
            _player.GlobalPosition = _center+Vector3.Up*.04f; _player.ResetPhysicsInterpolation();
            _overview = new Camera3D { Name = "FrontInspection", Fov = 44 };
            AddChild(_overview);
            BuildHud();
            while (MapManager.Instance?.IsReady != true) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            for (int i=0;i<3;i++) SpawnEnemy(i);
            _target = _enemies[0];
            _ready = true;
            if (_verify) await Verify();
        }
        catch (Exception error) { Check(false,error.ToString()); }
        if (_verify || _fail>0)
        {
            GetTree().Paused = false;
            Directory.CreateDirectory(ProjectSettings.GlobalizePath(Output));
            File.WriteAllText(ProjectSettings.GlobalizePath(Output+"validation.json"),JsonSerializer.Serialize(new { passed=_fail==0, pass=_pass, fail=_fail, checks=_checks, glow_samples=_glowSamples },new JsonSerializerOptions { WriteIndented=true }));
            GD.Print($"[CloverCombat] SUMMARY pass={_pass} fail={_fail}");
            GameExit.Request(GetTree(),_fail==0 ? 0 : 1);
        }
    }

    private void SpawnEnemy(int index)
    {
        var unit=RuntimeResources.Load<PackedScene>("res://Scenes/Enemies/Chaser.tscn").Instantiate<Unit>();
        var row=(EnemyConfig)ConfigRows.ById<EnemyConfig>("enemy_chaser")!.Duplicate();
        if (_verify) row.MaxHealth=10000;
        row.ApplyTo(unit);
        Vector3 desired=_center+new Vector3((index-1)*3,0,-6-index*2);
        if (!MapManager.Instance!.TryValidPoint(desired,_center,out var spawn,unit)) throw new InvalidOperationException("森林试装敌人未通过地面与净空检查");
        unit.Position=spawn+Vector3.Up*.05f;
        AddChild(unit);
        if (_verify) unit.GetNode<Node>("Brain").ProcessMode=ProcessModeEnum.Disabled;
        _enemies.Add(unit);
    }

    private void BuildHud()
    {
        var layer=new CanvasLayer();AddChild(layer);
        var panel=new PanelContainer { Position=new Vector2(18,18), MouseFilter=Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat { BgColor=new Color(.025f,.06f,.045f,.9f),ContentMarginLeft=16,ContentMarginRight=16,ContentMarginTop=12,ContentMarginBottom=12 });
        layer.AddChild(panel);
        _status=new Label { Theme=RuntimeResources.Load<Theme>("res://Assets/UI/Themes/ui_theme.tres") };
        _status.AddThemeFontSizeOverride("font_size",18);panel.AddChild(_status);
        var crosshair=new Label { Text="+",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Ignore };
        crosshair.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);layer.AddChild(crosshair);
    }

    public override void _Process(double delta)
    {
        if (!_ready) return;
        if (_front)
        {
            _overview.GlobalPosition=_player.GlobalPosition+new Vector3(2.7f,1.8f,-4.7f);
            _overview.LookAt(_player.GlobalPosition+Vector3.Up*1.1f);
        }
        _status.Text=$"三叶旅童 · 猎手战斗试装 / {_weapon}\n"
            +$"动画 {Animator.CurrentAnimation}   三叶草释放闪光 {_display.ActiveModel?.GetNodeOrNull<UnitCastGlow>("CastGlow")?.PulseCount ?? 0} 次\n"
            +"WASD移动  Shift奔跑  空格跳跃  左/右键武技  Q后撤  E跃升\n"
            +"F2正面近景  F3敌人启停  F5复位  1能量掌  2飞刀  3弓\n"
            +"仅试装场景使用新外观；正式角色A配置保持原样";
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!_ready || inputEvent is not InputEventKey { Pressed:true,Echo:false } key) return;
        switch (key.PhysicalKeycode == Key.None ? key.Keycode : key.PhysicalKeycode)
        {
            case Key.F2:
                _front=!_front; _overview.Current=_front;
                if (_front) { Movement.LockFacing(this); _player.Rotation=Vector3.Zero; }
                else Movement.UnlockFacing(this);
                _player.GetNode<Node3D>("CameraRig").ProcessMode=_front ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
                if (!_front) NodeSearch.FindInSubtree<Camera3D>(_player)!.MakeCurrent();
                break;
            case Key.F3:
                _enemyPaused=!_enemyPaused;
                foreach (var enemy in _enemies) if (IsInstanceValid(enemy)) enemy.GetNode<Node>("Brain").ProcessMode=_enemyPaused ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
                break;
            case Key.F5: RuntimeResources.ChangeScene(GetTree(),"res://Scenes/Debug/CloverCombatLab.tscn");break;
            case Key.Key1: case Key.Kp1: Equip(WeaponPresetKind.EnergyPalm);break;
            case Key.Key2: case Key.Kp2: Equip(WeaponPresetKind.Crossbow);break;
            case Key.Key3: case Key.Kp3: Equip(WeaponPresetKind.Bow);break;
            default:return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void Equip(WeaponPresetKind weapon)
    {
        _weapon=weapon;RunSetup.Weapon=weapon;
        _loadout.Apply(_player,this,PlayerCharacterKind.Hunter,weapon);
    }
    private void ResetPosition()
    {
        _player.GlobalPosition=_center+Vector3.Up*.04f;_player.Velocity=Vector3.Zero;_player.Rotation=Vector3.Zero;_player.ResetPhysicsInterpolation();
    }
    private void AimAtTarget()
    {
        ResetPosition();
        _target.GlobalPosition=_center+new Vector3(0,.04f,-5);
        _target.Velocity=Vector3.Zero;_target.KnockbackResistance=1;_target.ResetPhysicsInterpolation();
        _player.GetNode<Node3D>("CameraRig").ProcessMode=ProcessModeEnum.Disabled;
        _overview.GlobalPosition=_center+new Vector3(0,2.7f,5);
        _overview.LookAt(_target.GlobalPosition+Vector3.Up*.9f);_overview.MakeCurrent();
        _overview.ResetPhysicsInterpolation();
    }
    private void Press(string action,bool down) => Input.ParseInputEvent(new InputEventAction { Action=action,Pressed=down });
    private async Task Tap(string action) { Press(action,true);await Wait(.025);Press(action,false); }
    private async Task TapKey(Key code, bool logicalOnly=false)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode=code,PhysicalKeycode=logicalOnly ? Key.None : code,Pressed=true });
        await Wait(.025);
        Input.ParseInputEvent(new InputEventKey { Keycode=code,PhysicalKeycode=logicalOnly ? Key.None : code,Pressed=false });
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds,true),SceneTreeTimer.SignalName.Timeout);
    private async Task<bool> Until(Func<bool> predicate,double seconds=2)
    {
        ulong end=Time.GetTicksMsec()+(ulong)(seconds*1000);
        while (!predicate() && Time.GetTicksMsec()<end) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        return predicate();
    }
    private void Check(bool ok,string message)
    {
        if(ok)_pass++;else _fail++;
        _checks.Add(new { passed=ok,message });GD.Print($"[CloverCombat] {(ok ? "PASS" : "FAIL")} {message}");
    }
    private async Task Shot(string name)
    {
        if (DisplayServer.GetName()=="headless") return;
        Directory.CreateDirectory(ProjectSettings.GlobalizePath(Output));
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GD.Print($"[CloverView] {name} camera={GetViewport().GetCamera3D()?.GetPath()} position={GetViewport().GetCamera3D()?.GlobalPosition} overview={_overview.GlobalPosition} yaw={_player.Rotation.Y}");
        Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath(Output+name+".png"))==Error.Ok,"实拍 "+name);
    }

    private async Task Verify()
    {
        await Wait(.5);AimAtTarget();await Wait(.25);
        Check(!_profile.Validate().Any(),"档案骨架、动画与悬浮挂点绑定有效");
        var glyph=(MeshInstance3D)_display.ActiveModel!.FindChild("CW_ForeheadClover",true,false);
        var eyes=(MeshInstance3D)_display.ActiveModel.FindChild("CW_JadeEyes",true,false);
        Check(glyph.GetActiveMaterial(0).GetRid()!=eyes.GetActiveMaterial(0).GetRid(),"额头独立实例材质，不把共享碧绿眼睛一起改亮");
        Check(Glow.TargetCount==5,"额头、背部三片叶和叶茎均绑定发光，共五块图案");
        Check(_player.Scale.IsEqualApprox(Vector3.One) && _player.GetNode<CollisionShape3D>("Body").Shape is CapsuleShape3D { Height:1.8f,Radius:.4f },"正式玩家碰撞与根尺度保留");
        Check(await Until(()=>Movement.IsGrounded && Animator.CurrentAnimation=="idle"),"实际静止播放idle");
        var speedSource=new object();
        _player.Stats.AddModifier(new StatModifier { Stat="MoveSpeedMultiplier",Operation=ModifierOperation.Multiply,Value=.3f,Source=speedSource });_player.RefreshStats();
        Press("move_right",true);await Wait(.35);
        Check(Animator.CurrentAnimation=="walk" && Movement.PlanarVelocity.Length()>.5f,"实际慢速移动播放walk");
        _player.Stats.RemoveBySource(speedSource);_player.RefreshStats();Press("sprint",true);await Wait(.3);
        Check(Animator.CurrentAnimation=="sprint","实际快速移动播放sprint");
        Press("move_right",false);Press("sprint",false);await Wait(.3);AimAtTarget();await Wait(.15);
        await Tap("jump");
        Check(await Until(()=>Animator.CurrentAnimation=="jump" && Movement.IsAirborne),"真实跳跃输入驱动jump姿态与物理升降");
        await Shot("jump");
        Check(await Until(()=>Movement.IsGrounded && Animator.CurrentAnimation=="idle",2),"真实接地后播放缓冲并返回待机");

        foreach(var weapon in new[] { WeaponPresetKind.EnergyPalm,WeaponPresetKind.Crossbow,WeaponPresetKind.Bow })
        {
            Key hotkey=weapon==WeaponPresetKind.EnergyPalm ? Key.Key1 : weapon==WeaponPresetKind.Crossbow ? Key.Key2 : Key.Key3;
            await TapKey(hotkey);AimAtTarget();await Wait(.3);
            Check(_weapon==weapon && _display.CurrentWeaponPath==ActRoguelike.Rewards.RunController.WeaponRow(weapon)!.ModelPath,"真实数字键切换武器："+hotkey+" → "+weapon);
            Ability ability=_player.AbilitySlots.Resolve("weapon_skill_1")!;
            float health=_target.Health;int pulses=Glow.PulseCount;
            Press("weapon_skill_1",true);
            if(ability.HoldToCharge)
            {
                await Wait(ability.ChargeBaseSecondsEffective+.06);
                Check(Glow.PulseCount==pulses,"蓄力中的ATK事件不冒充释放闪光："+weapon);
            }
            Press("weapon_skill_1",false);
            Check(await Until(()=>Glow.PulseCount>pulses),weapon+"真实释放帧触发额头闪光");
            float start=Glow.Strength;
            Check(start<.25f,weapon+"释放时从暗处开始，没有瞬间跳到最亮");
            Check(await Until(()=>Glow.Strength>.98f),weapon+"从暗平滑升到亮度峰值");
            float peak=Glow.Strength;
            var frontMaterial=(StandardMaterial3D)glyph.GetActiveMaterial(0);
            var backMesh=(MeshInstance3D)_display.ActiveModel!.FindChild("CW_CloverLeaf1",true,false);
            var backMaterial=(StandardMaterial3D)backMesh.GetActiveMaterial(0);
            Check(Mathf.IsEqualApprox(frontMaterial.EmissionEnergyMultiplier,backMaterial.EmissionEnergyMultiplier)
                && frontMaterial.EmissionEnergyMultiplier>Glow.Energy*.95f,weapon+"前后图案同步达到发光峰值");
            Check(await Until(()=>_target.Health<health),weapon+"真实投射物命中敌人并扣血");
            Check(Glow.PulseCount==pulses+1,weapon+"一次释放仅闪一次，多弹不重复闪");
            await Shot("attack-"+weapon);
            // 真实命中会暂停局部视觉时钟，应等待实际进入衰减，不能假定墙钟0.12秒等于特效进度。
            await Until(()=>Glow.Strength<peak && Glow.Strength>0,2);float falling=Glow.Strength;
            Check(falling<peak && falling>0,weapon+"峰值后缓动变暗，亮度不会硬切消失");
            _glowSamples.Add(new { weapon=weapon.ToString(), start, peak, falling });
            await Wait(Glow.Duration+.05);Check(Glow.Strength==0,weapon+"闪光自然衰减完成");
        }

        await TapKey(Key.Kp3);
        Check(_weapon==WeaponPresetKind.Bow,"小键盘3也能切换弓");
        await TapKey(Key.Key1,true);
        Check(_weapon==WeaponPresetKind.EnergyPalm,"仅逻辑键码的键盘事件也能正常切换");

        Equip(WeaponPresetKind.EnergyPalm);AimAtTarget();await Wait(.3);
        Ability charge=_player.AbilitySlots.Resolve("weapon_skill_2")!;
        int before=Glow.PulseCount;Press("weapon_skill_2",true);await Wait(.08);
        bool early=charge.AllowEarlyChargeRelease;charge.AllowEarlyChargeRelease=false;
        Press("weapon_skill_2",false);await Wait(.25);charge.AllowEarlyChargeRelease=early;
        Check(Glow.PulseCount==before,"未完成且不允许低蓄力释放时，没有释放闪光");
        await Wait(.3);Press("weapon_skill_2",true);await Wait(charge.ChargeBaseSecondsEffective+.06);
        Check(Glow.PulseCount==before,"按住蓄力不重复发光");Press("weapon_skill_2",false);
        Check(await Until(()=>Glow.PulseCount==before+1),"蓄力完成松手，在新播放的ATK帧发光");
        await Shot("charged-release");await Wait(.4);

        before=Glow.PulseCount;await Tap("skill_1");
        Check(await Until(()=>Glow.PulseCount==before+1),"后撤Dodge释放帧发光");
        await Wait(.6);Check(Glow.PulseCount==before+1,"Dodger结束帧不重复发光");
        AimAtTarget();before=Glow.PulseCount;await Tap("skill_2");
        Check(await Until(()=>Glow.PulseCount==before+1 && Movement.IsAirborne),"跃升Action真实起跳并发光");
        await Wait(1.4);AimAtTarget();await Wait(.2);

        Equip(WeaponPresetKind.Bow);await Wait(.25);before=Glow.PulseCount;Press("weapon_skill_2",true);
        Check(await Until(()=>Glow.PulseCount==before+1),"无动画的维持技能成功启动也发光");
        await Wait(.4);Check(Glow.PulseCount==before+1,"维持技能按住期间不每帧重闪");Press("weapon_skill_2",false);await Wait(.3);

        Equip(WeaponPresetKind.EnergyPalm);AimAtTarget();await Wait(.3);before=Glow.PulseCount;await Tap("weapon_skill_1");
        Check(await Until(()=>Glow.PulseCount>before),"暂停检查前真实出手");
        Check(await Until(()=>Glow.Strength>.3f),"暂停检查覆盖正在渐亮的可见脉冲");
        float strength=Glow.Strength;GetTree().Paused=true;await Wait(.12);
        Check(Mathf.IsEqualApprox(Glow.Strength,strength),"全局暂停冻结闪光时钟");GetTree().Paused=false;
        await Wait(.4);
        // 正面近景仍由真实输入发招，只改变观察镜头，不伪造技能事件。
        _front=true;Movement.LockFacing(this);_player.Rotation=Vector3.Zero;_overview.MakeCurrent();await Wait(.1);await Tap("weapon_skill_1");
        Check(await Until(()=>Glow.Strength>.3f),"正面近景由真实释放触发图案发光");
        await Shot("forehead-glow");_front=false;Movement.UnlockFacing(this);await Wait(.4);
        UnitCastGlow oldGlow=Glow;
        // 旅童已成为正式档案，切换测试必须明确选择另一套模型，不能再假定“原档案”必然不同。
        _row.VisualProfile=_originalProfile?.ModelScene?.ResourcePath==_profile.ModelScene?.ResourcePath ? RuntimeResources.Load<UnitVisualProfile>("res://Config/VisualProfiles/lyra.tres") : _originalProfile;
        Equip(WeaponPresetKind.EnergyPalm);await Wait(.2);
        Check(!oldGlow.GetParent<Node3D>().Visible && oldGlow.Strength==0,"切换至另一模型时旧图案无残留发光");
        _row.VisualProfile=_profile;Equip(WeaponPresetKind.EnergyPalm);await Wait(.2);
        Check(Glow==oldGlow && Glow.Strength==0,"切回旅童复用实例且保持干净状态");

        AimAtTarget();await Wait(.2);
        var attack=new AttackHitbox { Damage=8,Knockback=1,CollisionMask=_player.CollisionLayer };
        attack.AddChild(new CollisionShape3D { Shape=new SphereShape3D { Radius=.75f } });
        AddChild(attack);attack.GlobalPosition=_player.GlobalPosition+Vector3.Up*.9f;await Wait(.05);
        float prior=_player.Health+_player.Armor;attack.Activate(_target);await Wait(.04);
        Check(_player.Health+_player.Armor<prior && Animator.CurrentAnimation=="hit","敌方真实命中扣血并播放站立受击");
        attack.Deactivate();await Shot("hit");attack.QueueFree();await Wait(.55);
        Check(Animator.CurrentAnimation=="idle","受击姿态结束回到待机");
        var fatal=new AttackHitbox { Damage=1000000,CollisionMask=_player.CollisionLayer };
        fatal.AddChild(new CollisionShape3D { Shape=new SphereShape3D { Radius=.8f } });
        AddChild(fatal);fatal.GlobalPosition=_player.GlobalPosition+Vector3.Up*.9f;await Wait(.05);fatal.Activate(_target);await Wait(.1);
        Check(_player.IsDead && Animator.CurrentAnimation=="die" && Glow.Strength==0,"真实致死命中播放死亡并清除闪光");
        fatal.QueueFree();await Wait(.7);await Shot("death");
    }

    public override void _ExitTree()
    {
        if (_row!=null) _row.VisualProfile=_originalProfile;
        foreach(string action in new[] { "move_right","sprint","jump","weapon_skill_1","weapon_skill_2","skill_1","skill_2" }) Input.ActionRelease(action);
    }
}
