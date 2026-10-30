"""首版场景符文资源与图标生成；已有资源不覆盖，后续直接在Godot编辑配置。"""
from pathlib import Path
import json
import re

root=Path(__file__).resolve().parents[1]
folder=root/"Config/Tables/SceneRunes"
if folder.exists() and any(folder.glob("*.tres")): raise SystemExit("已有场景符文，停止覆盖。")
ledger=json.loads((root/"Config/content-id-ledger.json").read_text(encoding="utf-8"))
start=max(v["number"] for v in ledger.values())+1
rows=[
 ("快乐飞弹·弹开","Happy Missile: Push",0,0,"每{Interval:0}秒发射一枚只碰玩家的穿墙飞弹，命中将玩家推开。","A wall-piercing missile launches every {Interval:0}s and pushes the player on contact.","#FFAD69",{"Interval":10,"MinDistance":10,"MaxDistance":16,"Radius":.65,"MaxActive":1}),
 ("快乐飞弹·治疗","Happy Missile: Heal",0,1,"每{Interval:0}秒发射穿墙飞弹，命中为玩家恢复{Heal:0}生命。","Every {Interval:0}s, a wall-piercing missile heals the player for {Heal:0} on contact.","#80E7A8",{"Interval":12,"MinDistance":10,"MaxDistance":16,"Radius":.65,"MaxActive":1}),
 ("快乐飞弹·护盾","Happy Missile: Shield",0,2,"每{Interval:0}秒发射穿墙飞弹，命中给予最大生命{ShieldRatio:0%}的护盾，持续{ShieldSeconds:0}秒。","Every {Interval:0}s, a wall-piercing missile grants a {ShieldRatio:0%} maximum-health shield for {ShieldSeconds:0}s.","#7AD9FF",{"Interval":12,"MinDistance":10,"MaxDistance":16,"Radius":.65,"MaxActive":1}),
 ("快乐飞弹·冲击","Happy Missile: Impact",0,3,"每{Interval:0}秒发射穿墙飞弹，仅命中玩家并造成{Damage:0}伤害；可跳跃或位移躲避。","Every {Interval:0}s, a wall-piercing missile deals {Damage:0} damage to the player. Jump or dodge to avoid it.","#FF8D89",{"Interval":12,"MinDistance":10,"MaxDistance":16,"Radius":.65,"MaxActive":1}),
 ("浮空场","Updraft",1,0,"周期生成半径{Radius:0.#}米的浮空地板，触及的地面单位被弹起约{LaunchHeight:0.#}米。","Periodic {Radius:0.#}m fields launch grounded units about {LaunchHeight:0.#}m upward.","#B8AEFF",{"Interval":16,"LaunchHeight":3,"MaxActive":1}),
 ("力场","Power Field",2,0,"周期生成半径{Radius:0.#}米的力场，玩家在圈内造成的伤害×{DamageMultiplier:0.##}，离开立即结束。","Periodic {Radius:0.#}m fields multiply the player's damage by {DamageMultiplier:0.##} while inside.","#FFDA76",{"Interval":16,"DamageMultiplier":1.25,"MaxActive":1}),
 ("治疗泉水","Healing Spring",3,0,"周期生成半径{Radius:0.#}米的泉水，范围内所有单位每秒恢复{HealPerSecond:0}生命。","Periodic {Radius:0.#}m springs restore {HealPerSecond:0} health per second to all units inside.","#73E8C7",{"Interval":16,"HealPerSecond":6,"MaxActive":1}),
 ("星雨","Starfall",4,0,"每{Interval:0}秒在可达地面预告一颗小陨石，落地造成半径{Radius:0.#}米、{Damage:0}伤害，只伤敌人。","Every {Interval:0}s, a small meteor strikes marked reachable ground for {Damage:0} damage in {Radius:0.#}m. Enemies only.","#FEBA76",{"Interval":5,"Radius":1.8,"Damage":35,"Speed":16,"MinDistance":2,"MaxDistance":14,"MaxActive":2}),
]
folder.mkdir(parents=True,exist_ok=True)
icons=root/"Assets/UI/Icons/SceneRunes";icons.mkdir(parents=True,exist_ok=True)
refs=[];zh={};en={}
symbols=["M18 34 L31 22 L46 18 L42 33 L29 45 Z M31 32 L39 25", "M32 19 V45 M19 32 H45", "M32 15 L47 21 V32 Q47 44 32 50 Q17 44 17 32 V21 Z", "M20 20 L44 44 M44 20 L20 44", "M20 37 L32 22 L44 37 M24 46 L32 36 L40 46", "M32 15 L43 29 L32 49 L21 29 Z", "M32 15 Q49 33 46 40 Q42 52 28 48 Q12 42 20 31 Z", "M35 14 L40 25 L51 28 L42 36 L43 48 L32 42 L21 48 L23 35 L14 27 L27 25 Z"]
for n,(name,english,kind,impact,desc,eng_desc,color,values) in enumerate(rows):
    number=start+n; icon=f"res://Assets/UI/Icons/SceneRunes/icon_{number}.svg"; source=f"res://Config/Tables/SceneRunes/scene_rune_{number}.tres"
    (icons/f"icon_{number}.svg").write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64"><rect x="3" y="3" width="58" height="58" rx="14" fill="#142633" stroke="{color}" stroke-width="2"/><path d="{symbols[n]}" fill="none" stroke="{color}" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/></svg>\n',encoding="utf-8")
    lines=['[gd_resource type="Resource" script_class="SceneRuneDefinition" format=3]', '[ext_resource type="Script" path="res://Scripts/Config/SceneRuneDefinition.cs" id="row"]', f'[ext_resource type="Texture2D" path="{icon}" id="icon"]','[resource]','script = ExtResource("row")',f'NumericId = {number}',f'DisplayName = {json.dumps(name,ensure_ascii=False)}',f'NameKey = "scene_rune.{number}.name"',f'DescriptionKey = "scene_rune.{number}.description"',f'DescriptionTemplate = {json.dumps(desc,ensure_ascii=False)}',f'Kind = {kind}',f'Impact = {impact}','Icon = ExtResource("icon")']
    rgb=[int(color[k:k+2],16)/255 for k in (1,3,5)];lines.append('Tint = Color('+', '.join(f'{v:.6f}' for v in rgb)+', 1)')
    lines.extend(f'{k} = {v}' for k,v in values.items());(folder/f"scene_rune_{number}.tres").write_text('\n'.join(lines)+'\n',encoding="utf-8")
    refs.append((f"scene_rune_{number}",source))
    zh.update({f"scene_rune.{number}.name":name,f"scene_rune.{number}.description":desc});en.update({f"scene_rune.{number}.name":english,f"scene_rune.{number}.description":eng_desc})
for relative,property_name in [("Config/ConfigIndex.tres","SceneRunes"),("Config/Modes/twilight_survival.tres","SceneRunePool")]:
    file=root/relative;source=file.read_text(encoding="utf-8-sig")
    declarations='\n'.join(f'[ext_resource type="Resource" path="{path}" id="{key}"]' for key,path in refs)
    position=min(p for p in [source.find('[sub_resource'),source.find('[resource]')] if p>=0)
    source=source[:position]+declarations+'\n\n'+source[position:]
    source += '\n'+property_name+' = ['+', '.join(f'ExtResource("{key}")' for key,_ in refs)+']\n'
    source=re.sub(r'load_steps=(\d+)',lambda m:'load_steps='+str(int(m[1])+len(refs)),source,count=1)
    file.write_text(source,encoding="utf-8")
for locale,entries in [("zh",zh),("en",en)]:
    file=root/f"Config/Localization/{locale}.tres";source=file.read_text(encoding="utf-8-sig")
    source=source.replace('Messages = {','Messages = {\n'+''.join(f'  {json.dumps(k)}: {json.dumps(v,ensure_ascii=False)},\n' for k,v in entries.items()),1)
    file.write_text(source,encoding="utf-8")
print(f"Created 8 scene runes: {start}..{start+7}")
