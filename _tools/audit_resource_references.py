"""只读列出资源引用证据；无引用仅代表待复核，永远不自动删除文件。"""

import argparse
import collections
import json
import re
import subprocess
import sys
from pathlib import Path


TEXT_SUFFIXES = {
    ".cs", ".gd", ".tscn", ".tres", ".godot", ".md", ".txt", ".json",
    ".ps1", ".py", ".cfg", ".ini", ".import", ".uid", ".gdshader",
    ".gdshaderinc", ".gltf", ".csproj", ".props", ".targets",
}
RESOURCE_SUFFIXES = {
    ".tscn", ".tres", ".glb", ".gltf", ".png", ".jpg", ".svg", ".webp",
    ".wav", ".ogg", ".mp3", ".ttf", ".otf", ".gdshader", ".gdshaderinc",
}
PATH_PATTERN = re.compile(r"res://[^\s\"'<>`\]\[),;]+")
UID_PATTERN = re.compile(r"uid://[a-z0-9]+")
NAME_PATTERN = re.compile(r'''["']([A-Za-z][A-Za-z0-9_./-]*)["']''')
DEFINITION_PATTERN = re.compile(r'^\s*(?:Id|resource_name)\s*=\s*"([^"\n]+)"', re.M)


def git_files(root, *args):
    data = subprocess.check_output(["git", "-C", str(root), "ls-files", "-z", *args])
    return set(data.decode("utf-8").rstrip("\0").split("\0")) - {""}


def source_kind(path):
    if path.startswith(("docs/", "ArtSource/")) or path.endswith(".md"):
        return "documentation"
    if path.startswith(("Scripts/Testing/", "Scenes/Debug/", "Config/Testing/", "_tools/")):
        return "test_or_tool"
    if path.startswith("Config/Retired/"):
        return "retired_dependency"
    return "production_or_authoring"


def audit(root):
    tracked = git_files(root)
    untracked = git_files(root, "--others", "--exclude-standard")
    files = tracked | untracked
    resources = sorted(path for path in files if path.startswith(("Assets/", "Scenes/", "Config/"))
                       and Path(path).suffix.lower() in RESOURCE_SUFFIXES and (root / path).is_file())
    resource_set = set(resources)
    texts = {}
    uid_targets = collections.defaultdict(set)
    name_targets = collections.defaultdict(set)
    for path in sorted(files):
        local = root / path
        if local.suffix.lower() not in TEXT_SUFFIXES or not local.is_file():
            continue
        # 限制读取大小；模型、图片和日志不参与文本解码。
        if local.stat().st_size > 4 * 1024 * 1024:
            continue
        try:
            body = local.read_text(encoding="utf-8-sig")
        except (UnicodeError, OSError):
            continue
        texts[path] = body
        if path in resource_set:
            name_targets[Path(path).stem].add(path)
            for name in DEFINITION_PATTERN.findall(body):
                name_targets[name].add(path)
            # 只接受资源首行自己的 UID，不把 ext_resource UID 当定义。
            for uid in UID_PATTERN.findall(body.splitlines()[0] if body else ""):
                uid_targets[uid].add(path)
        elif path.endswith(".uid"):
            for uid in UID_PATTERN.findall(body):
                uid_targets[uid].add(path[:-4])

    references = collections.defaultdict(list)
    directories = []
    templates = []
    for source, body in texts.items():
        kind = source_kind(source)
        # 工具自己的正则和说明不应成为资产引用证据。
        if source == "_tools/audit_resource_references.py":
            continue
        for line_number, line in enumerate(body.splitlines(), 1):
            targets = collections.defaultdict(set)
            # 只保守标出插值路径可能覆盖的资产，不声称已验证动态表达式执行。
            for template in re.findall(r'res://([^"\r\n]+)"', line):
                if kind != "documentation" and ("{" in template or "%s" in template):
                    pieces = re.split(r'\{[^}]*\}|%s', template)
                    if pieces[0] and all("{" not in piece and "}" not in piece for piece in pieces):
                        matcher = re.compile("^" + ".*".join(re.escape(piece) for piece in pieces) + "$")
                        templates.append((matcher, template, source, line_number, kind))
            for match in PATH_PATTERN.findall(line):
                path = match[6:]
                if "{" in path or "$" in path:
                    continue
                if path.endswith("/") and kind != "documentation":
                    directories.append((path, source, line_number, kind))
                elif path in resource_set:
                    targets[path].add("literal_path")
            for uid in UID_PATTERN.findall(line):
                for path in uid_targets.get(uid, ()):
                    if path in resource_set:
                        targets[path].add("uid")
            for name in NAME_PATTERN.findall(line):
                for path in name_targets.get(name, ()):
                    targets[path].add("id_or_name")
            for path, methods in targets.items():
                if path != source:
                    references[path].append({"source": source, "line": line_number,
                                             "kind": kind, "via": sorted(methods)})

    records = []
    for path in resources:
        evidence = references[path]
        directory_evidence = [{"source": source, "line": line, "kind": kind, "directory": prefix}
                              for prefix, source, line, kind in directories if path.startswith(prefix)]
        template_evidence = [{"source": source, "line": line, "kind": kind, "template": template}
                             for matcher, template, source, line, kind in templates if matcher.match(path)]
        kinds = {item["kind"] for item in evidence}
        if "production_or_authoring" in kinds:
            category = "production_or_authoring_reference"
        elif directory_evidence or template_evidence:
            category = "directory_convention_review"
        elif kinds & {"test_or_tool", "retired_dependency"}:
            category = "test_or_retired_dependency"
        elif evidence:
            category = "documentation_evidence_only"
        else:
            category = "no_literal_reference_review_required"
        records.append({"path": path, "git": "tracked" if path in tracked else "untracked_protected",
                        "category": category, "references": evidence, "directory_evidence": directory_evidence,
                        "template_evidence": template_evidence})
    return {
        "root": str(root),
        "limitations": "文本证据不是运行可达性证明；名称命中可能同名，目录命中需人工检查。未跟踪与外部工作始终保护。不会删除文件。",
        "inventory": {"tracked_files": len(tracked), "untracked_files": len(untracked),
                      "resource_files": len(resources), "text_files_read": len(texts)},
        "categories": dict(collections.Counter(item["category"] for item in records)),
        "resources": records,
    }


def main():
    # Windows 管道默认代码页可能不是 UTF-8；输出统一，方便后续脚本读取。
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--path", action="append", default=[], help="仅输出指定资源的证据，可重复")
    parser.add_argument("--json", action="store_true", help="向标准输出写完整 JSON，不写入工程文件")
    args = parser.parse_args()
    result = audit(args.root.resolve())
    if args.path:
        result["resources"] = [item for item in result["resources"] if item["path"] in args.path]
    elif not args.json:
        result.pop("resources")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
