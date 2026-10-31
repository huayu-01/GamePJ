"""在实机验证结束后整理柔和草地证据，编码不与基准采样同时运行。"""
from pathlib import Path
import json
import statistics
import subprocess
import sys
import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont, ImageOps

root = Path(__file__).resolve().parents[1]
directory = root / 'Logs/evidence/grass-soft'
directory.mkdir(parents=True, exist_ok=True)
for name in ('before', 'follow_ground', 'follow_grass', 'overview_ground', 'overview_grass', 'wall', 'style', 'detail', 'playable', 'angle-0', 'angle-45', 'angle-90'):
    source = root / f'Logs/evidence/elites/soft-{name}.png'
    if source.exists():
        with Image.open(source) as picture:
            picture.save(directory / f'{name}.png')
font = ImageFont.truetype(str(root / 'Assets/Fonts/NotoSansSC/NotoSansSC.ttf'), 25)
board = Image.new('RGB', (1920, 584), (18, 28, 25))
draw = ImageDraw.Draw(board)
for index, (path, label) in enumerate(((root / 'Logs/evidence/grass-jungle/follow_grass.png', '上一版：独立硬叶片'), (directory / 'follow_grass.png', '新版：十字草片、视角渐隐、草根同色'))):
    draw.text((index * 960 + 16, 5), label, font=font, fill='white')
    with Image.open(path) as source:
        board.paste(source.resize((960, 540), Image.Resampling.LANCZOS), (index * 960, 44))
board.save(directory / 'comparison.png')
if '--stills-only' not in sys.argv:
    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    def encode(arguments):
        result = subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', *arguments], capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError(result.stderr[-3000:])
    encode(['-framerate', '30', '-i', str(directory / 'frames/%03d.png'), '-an', '-vf', 'scale=1280:-2:flags=lanczos',
            '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(directory / 'wind.mp4')])
    encode(['-i', str(directory / 'wind.mp4'), '-filter_complex',
            'fps=20,scale=960:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=128[p];[b][p]paletteuse=dither=bayer',
            '-loop', '0', str(directory / 'wind.gif')])
if (directory / 'benchmark.json').exists():
    data = json.loads((directory / 'benchmark.json').read_text(encoding='utf-8-sig'))
    rows = []
    for variant in ('follow_ground', 'follow_grass', 'overview_ground', 'overview_grass'):
        phases = [phase for phase in data['phases'] if phase['variant'] == variant]
        row = {'variant': variant}
        for field in ('meanFrameMs', 'medianFrameMs', 'p95FrameMs', 'gpuMs', 'renderCpuMs', 'drawCalls', 'primitives'):
            row[field] = round(statistics.mean(phase[field] for phase in phases), 4)
        rows.append(row)
    (directory / 'summary.json').write_text(json.dumps({'rows': rows, 'normal': data['grassNormal'], 'chunks': data['chunks'],
        'gpu': data['gpu'], 'resolution': data['resolution'], 'trianglesPerClump': data['trianglesPerClump'], 'note': data['note']}, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(rows, ensure_ascii=False, indent=2))
