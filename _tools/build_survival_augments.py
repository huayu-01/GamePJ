"""十二项生存增幅的初始制作源；参数、文案、图标与显式索引一起生成。"""
from pathlib import Path
import json, re

ROOT = Path(__file__).resolve().parents[1]

# 旧制作源会覆盖现有身份、数量基线并重新启用退用构筑，禁止作为日常工具运行。
raise SystemExit("十二项死战专属构筑已于2026-10-28移除；本历史制作源停止执行。新增生存构筑需另行设计。")
def write(path, text):
    target=ROOT/path; target.parent.mkdir(parents=True,exist_ok=True); target.write_text(text.strip()+'\n',encoding='utf-8')
def q(value): return json.dumps(value,ensure_ascii=False)

# 属性依次传入对应行为的构造器，运行期无名称分支。
cards=[
('scavenger_compass','拾荒罗盘','ScavengerCompass','ScavengerCompassRule',0,[],[], [('RadiusScale',1.5),('SpeedScale',1.25)], '经验拾取半径×{Effects.0.RadiusScale:0.##}，吸收速度×{Effects.0.SpeedScale:0.##}。', '<circle cx="32" cy="32" r="23"/><path d="M32 10 43 32 32 54 21 32Z M10 32h10m24 0h10"/>'),
('energy_echo','采能回路','EnergyEcho','EnergyEchoRule',2,['scavenger_compass'],[], [('Threshold',20),('Radius',3),('DamageScale',.4)], '吸收每{Effects.0.Threshold:0}点原始敌人经验充能一次；下一次主武技命中产生{Effects.0.Radius:0.#}米回响，造成该次基础伤害的{Effects.0.DamageScale:0%}。最多储备一次。','<circle cx="32" cy="32" r="8"/><path d="M17 17a21 21 0 0 0 0 30m30-30a21 21 0 0 1 0 30M8 32h8m32 0h8"/>'),
('scavenger_ward','流转护膜','ScavengerWard','ScavengerWardRule',2,['scavenger_compass'],[], [('Threshold',20),('ShieldRatio',.05),('CapRatio',.1),('Duration',4)], '每吸收{Effects.0.Threshold:0}点原始敌人经验获得最大生命{Effects.0.ShieldRatio:0%}的护盾，持续{Effects.0.Duration:0.#}秒，此来源上限{Effects.0.CapRatio:0%}；抵挡不产生格挡硬直。','<path d="M32 7 52 15v18c0 12-20 24-20 24S12 45 12 33V15Z M23 32l7 7 13-16"/>'),
('anchor_core','驻点核心','AnchorCore','AnchorCoreRule',2,[],[], [('StationarySeconds',1),('Radius',5),('DamageBonus',.15),('ExpirySeconds',3)], '地面停留{Effects.0.StationarySeconds:0.#}秒建立{Effects.0.Radius:0.#}米锚圈，圈内直接攻击伤害+{Effects.0.DamageBonus:0%}；离圈{Effects.0.ExpirySeconds:0.#}秒消失。','<ellipse cx="32" cy="43" rx="24" ry="11"/><path d="M32 8v35M19 25l13-17 13 17M20 38l12 12 12-12"/>'),
('anchor_pulse','净域震荡','AnchorPulse','AnchorPulseRule',2,['anchor_core'],[], [('Interval',2),('Radius',2.8),('DamageScale',.35),('Push',2)], '圈内每{Effects.0.Interval:0.#}秒准备脉冲，下一次主武技命中造成{Effects.0.Radius:0.#}米、基础伤害{Effects.0.DamageScale:0%}的冲击并轻推普通敌人。','<circle cx="32" cy="32" r="8"/><circle cx="32" cy="32" r="17"/><path d="M7 22a27 27 0 0 0 0 20m50-20a27 27 0 0 1 0 20"/>'),
('anchor_afterglow','守望余火','AnchorAfterglow','AnchorAfterglowRule',0,['anchor_core'],[], [('RadiusBonus',1),('LingerSeconds',2)], '锚圈半径增加{Effects.0.RadiusBonus:0.#}米；离圈后直接攻击增伤保留{Effects.0.LingerSeconds:0.#}秒，不叠加。','<path d="M20 51c-18-13-9-31 4-35-2 13 6 15 8 19 10-10 1-23 8-28 18 18 23 34 5 45M29 49c-8-7-5-14 2-20 0 11 13 12 8 21"/>'),
('echo_familiar','回声灵偶','EchoFamiliar','EchoFamiliarRule',2,[],[], [('Interval',1.5),('DamageScale',.35),('Speed',30)], '主武技命中后两枚灵偶依次协击同一目标，每枚造成基础伤害{Effects.0.DamageScale:0%}；触发间隔{Effects.0.Interval:0.#}秒，每次出手最多一轮。','<path d="m18 9 10 16-10 16L8 25Zm28 0 10 16-10 16-10-16ZM18 45l14 11 14-11"/>'),
('echo_mark','共鸣刻印','EchoMark','EchoMarkRule',0,['echo_familiar'],['echo_lance'], [('Duration',4),('DamageBonus',.15)], '协击命中留下{Effects.0.Duration:0.#}秒刻印，玩家直接攻击刻印目标伤害+{Effects.0.DamageBonus:0%}，不叠加。','<path d="m32 6 23 26-23 26L9 32Z M18 32c8-12 20-12 28 0-8 12-20 12-28 0Z"/><circle cx="32" cy="32" r="4"/>'),
('echo_lance','集束协议','EchoLance','EchoLanceRule',1,['echo_familiar'],['echo_mark'], [('DamageScale',1.1),('Interval',2.2),('ExtraPierce',2)], '两枚协击合为冲针，造成基础伤害{Effects.0.DamageScale:0%}，再穿过{Effects.0.ExtraPierce:0}个敌人；触发间隔改为{Effects.0.Interval:0.#}秒。','<path d="m8 53 18-32L56 7 42 37Zm9-6 29-30M7 26l8 2M32 51l2 8"/>'),
('risk_contract','危险契约','RiskContract','RiskContractRule',2,[],[], [('ExperienceScale',1.2),('HealthDamageScale',1.1)], '敌人经验收益×{Effects.0.ExperienceScale:0.##}；实际承受的生命伤害×{Effects.0.HealthDamageScale:0.##}，不放大护甲和护盾损耗。','<path d="M15 7h28l8 8v42H15ZM43 7v12h10M35 23 24 38h10l-5 14 14-20H33Z"/>'),
('timed_hunt','刻限猎令','TimedHunt','TimedHuntRule',2,['risk_contract'],[], [('Interval',50),('Duration',15),('SpeedScale',1.2),('ExperienceReward',14)], '每{Effects.0.Interval:0}秒标记一只可达普通敌人{Effects.0.Duration:0}秒，其移速×{Effects.0.SpeedScale:0.##}；限时击败获得{Effects.0.ExperienceReward:0}额外经验。','<circle cx="32" cy="32" r="19"/><circle cx="32" cy="32" r="7"/><path d="M32 4v12m0 32v12M4 32h12m32 0h12M32 22v10l8 6"/>'),
('contract_payoff','破晓清算','ContractPayoff','ContractPayoffRule',0,['timed_hunt'],[], [('DamagePerCompletion',.03),('MaxCompletions',5)], '本局每完成一次刻限猎令，直接攻击伤害+{Effects.0.DamagePerCompletion:0%}，最多计{Effects.0.MaxCompletions:0}次。','<path d="M7 51h50M12 44l12-12 11 4 17-24M39 12h13v13"/><circle cx="18" cy="51" r="3"/><circle cx="32" cy="51" r="3"/><circle cx="46" cy="51" r="3"/>'),
]
for key,name,kind,rule,reward_kind,requires,excludes,props,description,icon in cards:
    fields=[]
    for field,value in props:
        integer=field in ('ExtraPierce','ExperienceReward','MaxCompletions')
        fields.append(f'\t[Export] public {"int" if integer else "float"} {field} {{ get; set; }} = {value}{"" if integer else "f"};')
    extra=''
    interface=''
    if key=='contract_payoff':
        interface=', IRewardRuntimeDetails'
        extra='\n\tpublic string DescribeRuntime() => RunController.Instance?.Mode is SurvivalDirector { BuildRuntime: { } runtime } ? runtime.Stopped ? "试炼战斗已结束" : $"本局完成合约：{runtime.Find<ContractPayoffRule>()?.Completed ?? 0}／{MaxCompletions}" : string.Empty;'
    source='''using System.Collections.Generic;
using ActRoguelike.Combat;
using ActRoguelike.Rewards;
using ActRoguelike.Run;
using Godot;

namespace ActRoguelike.Config;

[Tool, GlobalClass]
public partial class CLASS : SurvivalRewardEffectINTERFACE
{
FIELDS
\tprotected override IEnumerable<float> PositiveValues => new float[] { VALUES };
\tprotected override SurvivalAugmentRule CreateRule(SurvivalDirector director, Unit player) => new RULE(director, player, VALUES);EXTRA
}
'''.replace('CLASS',kind+'RewardEffect').replace('INTERFACE',interface).replace('FIELDS','\n'.join(fields)).replace('VALUES',', '.join(p[0] for p in props)).replace('RULE',rule).replace('EXTRA',extra)
    write(f'Scripts/Config/RewardEffects/Survival/{kind}RewardEffect.cs',source)
    data=['[gd_resource type="Resource" script_class="RewardConfig" format=3]', '[ext_resource type="Script" path="res://Scripts/Config/RewardConfig.cs" id="row"]', f'[ext_resource type="Script" path="res://Scripts/Config/RewardEffects/Survival/{kind}RewardEffect.cs" id="behavior"]', '[sub_resource type="Resource" id="effect"]', 'script = ExtResource("behavior")']
    data += [f'{field} = {value}' for field,value in props]
    data += ['[resource]','script = ExtResource("row")',f'Id = "surv_{key}"',f'DisplayName = {q(name)}',f'RewardKind = {reward_kind}','MaxStacks = 1','Weight = 1.0','RequiredModeIds = PackedStringArray("survival_twilight")',f'IconName = "ic_surv_{key}"','Effects = [SubResource("effect")]',f'DescriptionTemplate = {q(description)}',f'NameKey = "content.surv_{key}.name"',f'DescriptionKey = "content.surv_{key}.description"']
    if requires: data += ['Requires = PackedStringArray('+', '.join(q('surv_'+v) for v in requires)+')']
    if excludes: data += ['Excludes = PackedStringArray('+', '.join(q('surv_'+v) for v in excludes)+')']
    write(f'Config/Tables/Upgrades/surv_{key}.tres','\n'.join(data))
    write(f'Assets/UI/Icons/ic_surv_{key}.svg',f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" fill="none" stroke="#a8e8df" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">{icon}</svg>')

index=ROOT/'Config/ConfigIndex.tres'; text=index.read_text(encoding='utf-8-sig')
for key,*_ in cards:
    identity='surv_'+key
    if f'id="{identity}"' not in text:
        text=text.replace('[resource]',f'[ext_resource type="Resource" path="res://Config/Tables/Upgrades/{identity}.tres" id="{identity}"]\n\n[resource]',1)
    match=re.search(r'^Upgrades = \[(.*)\]$',text,re.M)
    if f'ExtResource("{identity}")' not in match[1]:
        text=text[:match.start()]+f'Upgrades = [{match[1]}, ExtResource("{identity}")]'+text[match.end():]
index.write_text(text,encoding='utf-8')
inventory=ROOT/'Config/Validation/content_inventory.tres'; text=inventory.read_text(encoding='utf-8-sig')
text=re.sub(r'"Upgrades": \d+', '"Upgrades": 103',text)
text=re.sub(r'^ExpectedRewardKinds = .*$', 'ExpectedRewardKinds = { "Enhancement": 27, "Evolution": 34, "Mutation": 42 }',text,flags=re.M)
inventory.write_text(text,encoding='utf-8')
mode=ROOT/'Config/Modes/twilight_survival.tres'; text=mode.read_text(encoding='utf-8-sig')
text=re.sub(r'^Id = "survival_twilight"\n','',text,flags=re.M)
text=text.replace('script = ExtResource("definition")','script = ExtResource("definition")\nId = "survival_twilight"')
mode.write_text(text,encoding='utf-8')
print('Created 12 survival augment resources, behaviors and icons; inventory 103.')
