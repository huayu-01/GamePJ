"""为内容声明稳定文本键并导出中文原文目录；默认预览，--apply 才写入。"""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GROUPS = ("Upgrades", "Runes", "Weapons", "Characters")


def field(text, name):
    match = re.search(rf"^{name} = (\"(?:[^\"\\]|\\.)*\")$", text, re.M)
    return json.loads(match[1]) if match else ""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--check", action="store_true")
    options = parser.parse_args()
    messages, changes = {}, {}
    for group in GROUPS:
        for path in sorted((ROOT / "Config/Tables" / group).glob("*.tres")):
            text = path.read_text(encoding="utf-8-sig")
            head, row = text.rsplit("[resource]", 1)
            identity = field(row, "Id")
            if not identity:
                raise ValueError(f"缺少 Id：{path}")
            for source, key_field, suffix in (("DisplayName", "NameKey", "name"), ("DescriptionTemplate", "DescriptionKey", "description")):
                key = field(row, key_field)
                if not key:
                    key = f"content.{identity}.{suffix}"
                    row += f'\n{key_field} = {json.dumps(key)}\n'
                if key in messages:
                    raise ValueError(f"重复文本键：{key}")
                messages[key] = field(row, source)
            updated = head + "[resource]" + row
            if updated != text:
                changes[path] = updated
    for path in sorted((ROOT / "Config/Modes").glob("*.tres")):
        row = path.read_text(encoding="utf-8-sig").rsplit("[resource]", 1)[-1]
        identity, title = field(row, "Id"), field(row, "Title")
        if identity and title: messages[f"mode.{identity}.name"] = title
    for path in sorted((ROOT / "Config/EliteTraits").glob("elite_*.tres")):
        row = path.read_text(encoding="utf-8-sig").rsplit("[resource]", 1)[-1]
        for source, key_field in (("Title", "NameKey"), ("Description", "DescriptionKey")):
            key = field(row, key_field)
            if not key or key in messages:
                raise ValueError(f"缺少或重复精英文本键：{path} / {key}")
            messages[key] = field(row, source)
    target = ROOT / "Config/Localization/zh.tres"
    source = ('[gd_resource type="Resource" script_class="ContentLocale" load_steps=2 format=3]\n'
              '[ext_resource type="Script" path="res://Scripts/Config/Localization/ContentLocale.cs" id="1"]\n'
              '[resource]\nscript = ExtResource("1")\nLocale = "zh"\nRequireComplete = true\nMessages = ')
    source += json.dumps(dict(sorted(messages.items())), ensure_ascii=False, indent=2) + "\n"
    if not target.exists() or target.read_text(encoding="utf-8-sig") != source:
        changes[target] = source
    if options.apply:
        for path, text in changes.items():
            path.resolve().relative_to(ROOT)
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open("w", encoding="utf-8", newline="\n") as output:
                output.write(text)
    print(f"文本条目 {len(messages)}，{'已更新' if options.apply else '待更新'}文件 {len(changes)}")
    return 1 if options.check and changes and not options.apply else 0


if __name__ == "__main__":
    raise SystemExit(main())
