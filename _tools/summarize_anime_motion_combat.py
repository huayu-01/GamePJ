"""汇总成对的森林实战样本，保留每阶段结果与原始逐帧CSV。"""
from pathlib import Path
import json
import sys
import statistics
import csv


ROOT = Path(__file__).resolve().parents[1]
directory = Path(sys.argv[1]) if len(sys.argv) > 1 else max(
    (ROOT / "Logs/evidence/anime-motion-combat").glob("run-*/benchmark.json"),
    key=lambda path: path.stat().st_mtime).parent
report = json.loads((directory / "benchmark.json").read_text(encoding="utf-8"))
with (directory / "frames.csv").open(encoding="utf-8", newline="") as stream:
    frames = list(csv.DictReader(stream))
rows = []
for cap in (1, 4, 8):
    off = [phase for phase in report["phases"] if phase["Limit"] == cap and not phase["Enabled"]]
    on = [phase for phase in report["phases"] if phase["Limit"] == cap and phase["Enabled"]]
    def average(phases, field, statistic="mean"):
        return statistics.mean(phase[field][statistic] for phase in phases)
    before = average(off, "frame_ms")
    after = average(on, "frame_ms")
    active_times = [float(frame["frame_ms"]) for frame in frames
                    if frame["enabled"] == "True" and int(frame["cap"]) == cap and int(frame["active_captures"]) == cap]
    rows.append({"effect_cap": cap, "off_frame_ms": before, "on_frame_ms": after,
                 "delta_frame_ms": after - before, "delta_frame_percent": (after / before - 1) * 100,
                 "off_fps_from_mean_frame": 1000 / before, "on_fps_from_mean_frame": 1000 / after,
                 "off_p95_ms": average(off, "frame_ms", "p95"), "on_p95_ms": average(on, "frame_ms", "p95"),
                 "off_gpu_ms": average(off, "gpu_total_ms"), "on_gpu_ms": average(on, "gpu_total_ms"),
                 "on_active_gpu_ms": average(on, "gpu_active_ms"),
                 "on_active_frame_ms": statistics.mean(active_times),
                 "on_active_frame_p95_ms": sorted(active_times)[int((len(active_times) - 1) * .95)],
                 "off_render_cpu_ms": average(off, "render_cpu_ms"), "on_render_cpu_ms": average(on, "render_cpu_ms"),
                 "off_draw_calls": average(off, "draw_calls"), "on_draw_calls": average(on, "draw_calls"),
                 "full_concurrency_frames": sum(phase["full_concurrency_frames"] for phase in on)})
summary = {"passed": report["passed"] and (directory / "validation.log").exists(), "gpu": report["gpu"], "cpu": report["cpu"],
           "resolution": report["resolution"], "build": report["build"], "rows": rows,
           "limits": ["本机开发构建，固定实战镜头和八名敌人，不代表所有硬件或完整关卡上限。",
                      "逐帧GPU计时存在回读延迟；动态捕获视口按当帧启用状态累计，作为开销估计。",
                      "全阶段平均包含动作间歇；同时触发窗口另列GPU平均和实际采样数量。",
                      "同一上限采用OFF ON ON OFF，每个阶段先预热0.4秒，四阶段分别等权汇总。"]}
(directory / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
lines = ["# 森林实战外轮廓模糊性能", "", f"GPU：{report['gpu']}；CPU：{report['cpu']}。",
         f"分辨率：{report['resolution']}；构建：{report['build']}；垂直同步：{report['vsync']}。", "",
         "| 效果人数上限 | 关闭平均ms | 开启平均ms | 增加ms | 关闭/开启FPS | 同时触发平均/p95 ms | GPU关闭/开启ms | 同时触发样本 |",
         "|---|---:|---:|---:|---|---|---|---:|"]
for row in rows:
    lines.append(f"| {row['effect_cap']} | {row['off_frame_ms']:.2f} | {row['on_frame_ms']:.2f} | {row['delta_frame_ms']:+.2f} | "
                 f"{row['off_fps_from_mean_frame']:.0f} / {row['on_fps_from_mean_frame']:.0f} | {row['on_active_frame_ms']:.2f} / {row['on_active_frame_p95_ms']:.2f} | "
                 f"{row['off_gpu_ms']:.2f} / {row['on_gpu_ms']:.2f} | {row['full_concurrency_frames']} |")
lines.extend(["", f"真实玩家受击：{report['actual_player_hits']}；敌人实际攻击事件：{report['executed_attacks']}。", ""])
lines.extend("- " + item for item in summary["limits"])
(directory / "report.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
print(json.dumps(summary, ensure_ascii=True, indent=2))
