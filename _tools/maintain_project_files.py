"""项目日志与历史文档整理；默认只预览，--apply执行已列出的迁移，--check检查链接。"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import sys
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
LINK = re.compile(r'(!?\[[^\]\n]*\]\()(<[^>\n]+>|[^\s)\n]+)([^)\n]*\))')
ARCHIVE_NAMES = {
    "art-integration-log.md", "art-refresh-handoff.md", "art-refresh-verification.md",
    "balance-review.md", "character-redesign.md", "controller-flow.md",
    "followup-plan.md", "level-core-doc-gap.md", "original-design-audit.md",
    "refactor-survey.md", "requirements-ability-system.md", "visual-inventory.md",
    "config-tables-inventory.md", "config-tooling-yard.md",
}
REDIRECTS = {"HANDOFF.md", "DEEPSEEK_HANDOFF.md", "todo-handoff.md", "roadmap-2.md", "roadmap-2-completion.md"}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def safe(path: Path) -> Path:
    # 任何删除或移动之前检查最终绝对路径，拒绝越过工程边界。
    resolved = path.resolve()
    if os.path.commonpath((str(resolved), str(ROOT))) != str(ROOT) or resolved == ROOT:
        raise ValueError(f"路径越界：{path}")
    return resolved


def write(path: Path, text: str) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        stream.write(text)


def markdown_files() -> list[Path]:
    files = [ROOT / "README.md"]
    for folder in ("docs", "Assets", "ArtSource", "Scenes", "Config", "_tools"):
        for directory, names, filenames in os.walk(ROOT / folder, followlinks=False):
            names[:] = [n for n in names if not n.startswith(".") and n not in {"node_modules", "__pycache__"}]
            files.extend(Path(directory) / n for n in filenames if n.endswith(".md"))
    return sorted(set(p for p in files if p.is_file()))


def target_of(source: Path, raw: str) -> tuple[Path, str] | None:
    value = raw.strip("<>")
    if not value or value.startswith("#") or re.match(r"[a-zA-Z][\w+.-]*://|mailto:|data:", value):
        return None
    value, mark, anchor = value.partition("#")
    if any(c in value for c in ("{", "}", "$", "*", "[", "]")):
        return None
    path = Path(value)
    if not path.is_absolute():
        path = source.parent / path
    try:
        if not path.suffix and not path.exists():
            return None
        return path.resolve(), (mark + anchor if mark else "")
    except OSError:
        # 历史文档中的代码示例可能看起来像Markdown链接，但不是合法文件名。
        return None


def broken_links(files: list[Path], texts: dict[Path, str], mapping: dict[Path, Path]) -> set[tuple[str, str]]:
    broken = set()
    for source in files:
        for match in LINK.finditer(texts[source]):
            parsed = target_of(source, match[2])
            if parsed and not parsed[0].exists():
                broken.add((str(mapping.get(source, source)), str(mapping.get(parsed[0], parsed[0]))))
    return broken


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    report_dir = ROOT / "Logs/maintenance" / stamp
    docs = markdown_files()
    texts = {p: p.read_text(encoding="utf-8-sig") for p in docs}
    if args.check:
        broken = sorted(broken_links(docs, texts, {}))
        print(json.dumps({"markdown_files": len(docs), "broken_links": len(broken), "examples": broken[:12]}, ensure_ascii=False, indent=2))
        if broken:
            raise SystemExit(1)
        return

    mapping: dict[Path, Path] = {}
    moves: list[tuple[Path, Path]] = []
    aliases: list[tuple[Path, Path]] = []
    for path in sorted((ROOT / "docs").glob("*.log")):
        target = ROOT / "Logs/evidence" / path.name
        mapping[path] = target
        moves.append((path, target))
    canonical = {digest(source): target for source, target in moves if source.stat().st_size}
    root_logs = sorted(p for p in ROOT.iterdir() if p.is_file() and p.suffix in {".log", ".err"})
    for path in root_logs:
        hashed = digest(path)
        if hashed in canonical:
            target = canonical[hashed]
            aliases.append((path, target))
        else:
            target = ROOT / "Logs/archive/root-20261024" / path.name
            moves.append((path, target))
            canonical[hashed] = target
        mapping[path] = target
    snapshots = sorted(p for p in ROOT.iterdir() if p.is_file() and p.name.endswith(".saves.json"))
    for path in snapshots:
        target = ROOT / "Logs/archive/root-20261024" / path.name
        mapping[path] = target
        moves.append((path, target))
    for path in sorted((ROOT / "docs").glob("*.md")):
        if re.fullmatch(r"character-design-j\d+\.md|[a-e]-batch-verification\.md|followup-[fgh]-verification\.md", path.name):
            target = ROOT / "docs/archive/development-batches" / path.name
        elif path.name in ARCHIVE_NAMES:
            target = ROOT / "docs/archive/design-notes" / path.name
        elif path.name in REDIRECTS:
            target = ROOT / "docs/archive/handoffs-2026-10-18" / path.name
            if not target.exists() or len(texts[path]) > 500 or "归档" not in texts[path]:
                raise ValueError(f"不是已替代的跳转页：{path}")
            mapping[path] = target
            aliases.append((path, target))
            continue
        else:
            continue
        mapping[path] = target
        moves.append((path, target))
    for source, target in moves:
        safe(source); safe(target)
        if target.exists():
            raise ValueError(f"目标已存在，拒绝覆盖：{target}")
    before_broken = broken_links(docs, texts, mapping)
    rewritten: dict[Path, str] = {}
    removed_docs = {source for source, _ in aliases if source.suffix == ".md"}
    for source in docs:
        if source in removed_docs:
            continue
        new_source = mapping.get(source, source)
        def rewrite(match: re.Match) -> str:
            parsed = target_of(source, match[2])
            if not parsed:
                return match[0]
            target, anchor = parsed
            new_target = mapping.get(target, target)
            if new_source == source and new_target == target:
                return match[0]
            value = os.path.relpath(new_target, new_source.parent).replace("\\", "/") + anchor
            if " " in value:
                value = "<" + value + ">"
            return match[1] + value + match[3]
        updated = LINK.sub(rewrite, texts[source])
        # 文档内显式工程路径也同步；日志原文不修改，保留取证内容。
        for old, new in mapping.items():
            if old.parent == ROOT / "docs":
                updated = updated.replace(relative(old), relative(new))
        if new_source != source:
            updated = "> 历史记录：仅用于追溯。本文件中的旧数量、待办与阶段结论不代表当前规则；当前入口见[开发快照](../../SESSION-SNAPSHOT.md)。\n\n" + updated
        if updated != texts[source]:
            rewritten[source] = updated

    scripts = list((ROOT / "_tools").glob("*.ps1"))
    scripts += [ROOT / "ArtSource/Tools/Capture-EnemyModels.ps1", ROOT / "ArtSource/Tools/Capture-StoneGolemPlus.ps1"]
    script_texts: dict[Path, str] = {}
    for source in scripts:
        if not source.exists() or source.name in {"LogPaths.ps1", "run_selftest.ps1"}:
            continue
        value = source.read_text(encoding="utf-8-sig")
        updated = re.sub(r"(?<![/\\\w])((?:\.ai_|\.run_ai_)[^\s'\"<>]*?\.(?:log|err|saves\.json))", r"Logs/validation/\1", value)
        updated = re.sub(r"docs[/\\](?=[^'\"\r\n]*?\.log)", "Logs/evidence/", updated)
        if updated != value:
            updated, inserted = re.subn(r"(\$projectDir = [^\r\n]+)",
                r"\1\n. (Join-Path $projectDir '_tools/LogPaths.ps1')\nInitialize-ProjectLogPaths -ProjectRoot $projectDir", updated, count=1)
            if inserted != 1:
                raise ValueError(f"日志目录缺初始化接线：{source}")
            script_texts[source] = updated.replace("\r\n", "\n")

    summary = {"root_logs": len(root_logs), "log_duplicates_removed": sum(s.suffix != ".md" for s, _ in aliases),
               "save_fingerprints_moved": len(snapshots),
               "unique_log_outputs_moved": sum(s.suffix in {".log", ".err"} for s, _ in moves),
               "documents_archived": sum(s.suffix == ".md" for s, _ in moves), "redirects_removed": len(removed_docs),
               "documents_relinked": len(rewritten), "log_writers_updated": len(script_texts),
               "existing_broken_links": len(before_broken)}
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    if not args.apply:
        return
    for source in set(rewritten) | set(script_texts) | removed_docs:
        safe(source)
        safe(mapping.get(source, source))
    report_dir.mkdir(parents=True)
    records = [{"operation": "move", "source": relative(s), "target": relative(t), "sha256": digest(s)} for s, t in moves]
    records += [{"operation": "remove_duplicate_or_redirect", "source": relative(s), "target": relative(t), "sha256": digest(s)} for s, t in aliases]
    manifest = {"summary": summary, "status": "planned", "files": records}
    manifest_path = report_dir / "manifest.json"
    write(manifest_path, json.dumps(manifest, ensure_ascii=False, indent=2))
    for source in set(rewritten) | set(script_texts) | removed_docs:
        backup = report_dir / "before" / relative(source)
        backup.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, backup)
    for source, target in moves:
        target.parent.mkdir(parents=True, exist_ok=True)
        source.rename(target)
    for source, target in aliases:
        safe(source); safe(target)
        if not target.is_file() or (source.suffix != ".md" and digest(source) != digest(target)):
            raise ValueError(f"尚未保留相同日志内容：{source}")
        source.unlink()
    for source, value in rewritten.items():
        write(mapping.get(source, source), value)
    for source, value in script_texts.items():
        write(source, value)
    after_docs = markdown_files()
    after_broken = broken_links(after_docs, {p: p.read_text(encoding="utf-8-sig") for p in after_docs}, {})
    added = sorted(after_broken - before_broken)
    manifest.update(status="complete" if not added else "needs_link_review", new_broken_links=added,
                    remaining_root_logs=len([p for p in ROOT.iterdir() if p.is_file() and p.suffix in {".log", ".err"}]))
    write(manifest_path, json.dumps(manifest, ensure_ascii=False, indent=2))
    print(f"清单：{relative(manifest_path)}；新增断链：{len(added)}")
    if added:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
