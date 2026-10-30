"""将 Godot 实录转成完整 MP4 和便于观察的慢放 GIF，不修改项目美术。"""
from pathlib import Path
import subprocess

import imageio_ffmpeg


ROOT = Path(__file__).resolve().parents[1]
DIRECTORY = ROOT / "Logs/evidence/anime-motion"


def run(arguments):
    result = subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-hide_banner", "-loglevel", "error", "-y", *arguments],
                            capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(result.stderr[-3000:])


for name, start, duration, slowdown in (("dash", 2.95, 0.90, 2.0), ("action", 2.95, 0.65, 3.0)):
    source = DIRECTORY / (name + ".avi")
    run(["-i", str(source), "-an", "-vf", "scale=1280:-2:flags=lanczos", "-c:v", "libx264",
         "-preset", "medium", "-crf", "19", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(DIRECTORY / (name + "-contour.mp4"))])
    filters = (f"[0:v]setpts={slowdown}*PTS,fps=24,scale=960:-2:flags=lanczos,split[a][b];"
               "[a]palettegen=max_colors=96:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3")
    run(["-ss", str(start), "-t", str(duration), "-i", str(source), "-an", "-filter_complex", filters,
         "-loop", "0", str(DIRECTORY / (name + "-contour.gif"))])
    print(name, "MP4 / GIF ready")
