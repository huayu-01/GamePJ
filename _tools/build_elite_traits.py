"""生成精英特性的显式目录和独立定义；参数调好后不要无意重跑覆盖。"""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
# 稳定Id、定义类、中文名、说明、参数。行为工厂在各自资源脚本中，不在注册器按Id分支。
ROWS = [
 ('shifter','Shifter','瞬移','周期性预警换位，忙动作和空中阶段不触发。',dict(Cooldown=7,Radius=5,Requirements=1)),
 ('bruiser','Bruiser','巨壮','体型与生命提高，最终身体参与导航净空。',dict(Magnitude=1.35,PreSpawnScale=1.2)),
 ('slugger','Slugger','强击','攻击伤害提高。',dict(Magnitude=1.3)),
 ('savior','Savior','庇护','周期授予附近友军一次性挡伤护罩。',dict(MaxCarriers=1,Cooldown=7,Radius=7,Count=2,Duration=4)),
 ('speeder','Speeder','迅捷','移动和恢复加快，保留攻击预警。',dict(Magnitude=1.25,Requirements=1)),
 ('linker','Linker','连线','与同波友军形成有预警的伤害连线。',dict(Cooldown=4,Radius=8,Count=1,Duration=4)),
 ('burner','Burner','燃烧','周期铺设短暂地面火焰。',dict(Cooldown=5,Radius=2,Duration=3,Damage=6)),
 ('seeker','Seeker','追踪','可改造直射弹先直飞，再有限追踪。',dict(Magnitude=75,Radius=1.8,Requirements=2)),
 ('burrower','Burrower','蔓生','预警后生成短暂限制行动的藤蔓。',dict(MaxCarriers=1,Cooldown=8,Radius=3,Duration=2)),
 ('clanger','Clanger','压境','固定同波保护对象死亡后解除无敌。',dict(MaxCarriers=1,ExtraCost=3)),
 ('slicer','Slicer','扫射','光束绕自身缓慢旋转。',dict(Cooldown=7,Radius=5,Duration=4,Magnitude=35)),
 ('spiller','Spiller','黑雾','进入暗影罩内才可伤害本体。',dict(MaxCarriers=1,Radius=5,Requirements=8,ExtraCost=3)),
 ('spinner','Spinner','环星','三颗伤害球环绕本体。',dict(Count=3,Radius=2.5,Damage=8,Magnitude=55)),
 ('arcane','Arcane','秘法强化','预警后放置固定节点旋转激光。',dict(MaxCarriers=1,Cooldown=8,Radius=5,Duration=4,Count=1,Magnitude=35,ExtraCost=3)),
 ('frozen','Frozen','冰冻','冰晶延迟爆炸，短暂冻结范围内玩家。',dict(Cooldown=8,Radius=2.5,Duration=1,Count=2,Damage=10)),
 ('molten','Molten','熔火','移动留火路，自然死亡后预警爆炸。',dict(Cooldown=1.5,Radius=1.3,Duration=2.5,Damage=6)),
 ('knockback','Knockback','击退','本体直接攻击间歇附加击退。',dict(Cooldown=1.2,Magnitude=5)),
 ('shielding','Shielding','间歇无敌','无敌与开放期交替，保护来源独立。',dict(MaxCarriers=1,Cooldown=8,Duration=2)),
 ('health_link','HealthLink','生命链接','同组合法对象分担伤害，转移不再递归。',dict(MaxCarriers=1,Radius=8,Magnitude=.35,Count=2)),
 ('avenger','Avenger','复仇','固定同波成员死亡后增强，最多三次。',dict(Magnitude=1.15,Count=3)),
 ('juggernaut','Juggernaut','强横','免普通控制，移速降低，保留持盾自身硬直。',dict(Magnitude=.85)),
 ('multishot','Multishot','多重射击','适用射击改为三发，总伤害受限。',dict(Count=3,Radius=40,Magnitude=1.35,Requirements=2)),
 ('summoner','Summoner','召唤','免费召唤有限一批普通随从，不补员。',dict(MaxCarriers=1,Cooldown=3,Count=3,ExtraCost=3)),
 ('suppressor','Suppressor','压制','周期阻挡来自罩外的指定远程投射物。',dict(MaxCarriers=1,Radius=4,Duration=3,Cooldown=8)),
 ('vampiric','Vampiric','吸血','本体直接攻击按实际生命伤害吸血。',dict(Magnitude=.25)),
 ('pressure_rebound','PressureRebound','蓄压回震','累计有效伤害后预警释放冲击波。',dict(Magnitude=100,Radius=3,Damage=12,Cooldown=5)),
 ('armor_recoil','ArmorRecoil','裂甲反冲','非致死破甲后预警反冲，再短暂易伤。',dict(Radius=3,Damage=10,Duration=2,Magnitude=1.25)),
 ('reforge','Reforge','重铸','破甲后可被打断地修甲，每只最多成功一次。',dict(Damage=60,Duration=2,MaxCarriers=1)),
 ('tidal','Tidal','潮汐领域','内圈与外环交替爆发，保留安全带。',dict(MaxCarriers=1,Cooldown=7,Radius=5,Duration=2,Damage=12)),
 ('aftershock','Aftershock','余震回廊','攻击实际路径预警后产生一次余震。',dict(Cooldown=4,Radius=1.3,Damage=12,Duration=.7,Requirements=4)),
 ('trail_brand','TrailBrand','疾行烙印','短暂标记玩家经过处，延迟产生有限爆点。',dict(Cooldown=7,Radius=1.2,Duration=3,Count=5,Damage=10)),
 ('flanker','Flanker','侧翼猎手','预警后沿真实可通行路线移动至侧面。',dict(Cooldown=7,Radius=4,Magnitude=6,Duration=1,Requirements=1)),
 ('thorn_grid','ThornGrid','荆棘棋盘','地板接触触发荆棘效果，跳跃可以躲避。',dict(MaxCarriers=1,Cooldown=7,Radius=1.7,Duration=4,Count=3,Damage=8)),
]

def build():
    scripts = ROOT / 'Scripts/Combat/Elites/Definitions'
    resources = ROOT / 'Config/EliteTraits'
    scripts.mkdir(parents=True, exist_ok=True)
    resources.mkdir(parents=True, exist_ok=True)
    entries = []
    for index, (key, kind, title, description, values) in enumerate(ROWS, 1):
        extra = ''
        if kind in ('Seeker', 'Multishot'):
            extra = f'\n\tpublic override bool Supports(Unit owner) => base.Supports(owner) && EliteProjectileRules.Can{("Seek" if kind == "Seeker" else "Multishot")}(owner);'
        if kind == 'Reforge':
            extra = '\n\tpublic override bool Supports(Unit owner) => base.Supports(owner) && owner.GetNodeOrNull<CharacterMovement>("Movement")?.PhysicsProfile?.ControlImmune != true && !owner.HasTraitControlImmunity;'
        script = f'''using Godot;
namespace ActRoguelike.Combat.Elites;
/// <summary>{description}</summary>
[Tool, GlobalClass]
public partial class {kind}TraitDefinition : EliteTraitDefinition
{{
\tpublic override EliteTraitBehavior Create(Unit owner, EliteTraitWave wave) => new {kind}TraitBehavior(this, owner, wave);{extra}
}}
'''
        (scripts / f'{kind}TraitDefinition.cs').write_text(script, encoding='utf-8')
        ext = f'[ext_resource type="Script" path="res://Scripts/Combat/Elites/Definitions/{kind}TraitDefinition.cs" id="1"]\n'
        if kind == 'Summoner':
            ext += '[ext_resource type="PackedScene" path="res://Scenes/Enemies/Chaser.tscn" id="2"]\n'
        lines = [f'[gd_resource type="Resource" script_class="{kind}TraitDefinition" load_steps={3 if kind == "Summoner" else 2} format=3]', '', ext.rstrip(), '', '[resource]', 'script = ExtResource("1")', f'Id = "elite_{key}"', f'Title = {json.dumps(title, ensure_ascii=False)}', f'Description = {json.dumps(description, ensure_ascii=False)}', f'NameKey = "elite.{key}.name"', f'DescriptionKey = "elite.{key}.description"']
        for field, value in values.items():
            lines.append(f'{field} = {value}')
        if kind == 'Summoner':
            lines.append('SummonScene = ExtResource("2")')
        (resources / f'elite_{key}.tres').write_text('\n'.join(lines) + '\n', encoding='utf-8')
        entries.append(f'[ext_resource type="Resource" path="res://Config/EliteTraits/elite_{key}.tres" id="{index}"]')
    catalog = [f'[gd_resource type="Resource" script_class="EliteTraitCatalog" load_steps={len(ROWS)+2} format=3]', '', '[ext_resource type="Script" path="res://Scripts/Combat/Elites/EliteTraitCatalog.cs" id="0"]', *entries, '', '[resource]', 'script = ExtResource("0")', f'ExpectedCount = {len(ROWS)}', 'Traits = Array[Resource]([' + ', '.join(f'ExtResource("{i}")' for i in range(1, len(ROWS)+1)) + '])', '']
    (resources / 'catalog.tres').write_text('\n'.join(catalog), encoding='utf-8')
    print('33 elite definitions and explicit catalog generated')

if __name__ == '__main__':
    build()
