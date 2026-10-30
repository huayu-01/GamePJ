"""实战录屏独立编码，禁止与性能采样同时执行。"""
from pathlib import Path
import subprocess
import sys
import imageio_ffmpeg

directory = Path(sys.argv[1])
ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()

def run(arguments):
    result = subprocess.run([ffmpeg, "-hide_banner", "-loglevel", "error", "-y", *arguments], capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(result.stderr[-3000:])

run(["-i", str(directory / "combat.avi"), "-an", "-vf", "scale=1280:-2:flags=lanczos",
     "-c:v", "libx264", "-crf", "19", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(directory / "combat.mp4")])
run(["-ss", "4.1", "-t", "1.6", "-i", str(directory / "combat.avi"), "-an", "-filter_complex",
     "[0:v]setpts=1.5*PTS,fps=24,scale=960:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=96[p];[b][p]paletteuse=dither=bayer",
     "-loop", "0", str(directory / "combat.gif")])
print("combat MP4 / GIF ready")
