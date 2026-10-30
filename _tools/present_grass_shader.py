"""对已完成的实机截图和记录帧做排版、编码，不与基准采样同时运行。"""
from pathlib import Path
import json
import statistics
import subprocess
import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
directory = root / 'Logs/evidence/grass-shader'
directory.mkdir(parents=True, exist_ok=True)
for name in ('ground', 'batched', 'individual', 'dense'):
    with Image.open(root / f'Logs/evidence/elites/grass-{name}.png') as source:
        source.save(directory / f'{name}.png')
font = ImageFont.truetype(str(root / 'Assets/Fonts/NotoSansSC/NotoSansSC.ttf'), 25)
board = Image.new('RGB', (1920, 584), (18, 28, 25))
draw = ImageDraw.Draw(board)
for index, (name, label) in enumerate((('ground', '原地面'), ('batched', '分块风动草地'))):
    draw.text((index * 960 + 16, 5), label, font=font, fill='white')
    with Image.open(directory / f'{name}.png') as source:
        board.paste(source.resize((960, 540), Image.Resampling.LANCZOS), (index * 960, 44))
board.save(directory / 'comparison.png')
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
data = json.loads((directory / 'benchmark.json').read_text(encoding='utf-8-sig'))
rows = []
for variant in ('Ground', 'Batched', 'Individual', 'Dense'):
    phases = [phase for phase in data['phases'] if phase['variant'] == variant]
    row = {'variant': variant}
    for field in ('meanFrameMs', 'medianFrameMs', 'p95FrameMs', 'gpuMs', 'renderCpuMs', 'drawCalls', 'primitives'):
        row[field] = round(statistics.mean(phase[field] for phase in phases), 4)
    rows.append(row)
(directory / 'summary.json').write_text(json.dumps({'rows': rows, 'normal': data['grassNormal'], 'dense': data['grassDense'],
    'gpu': data['gpu'], 'resolution': data['resolution'], 'note': data['note']}, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(rows, ensure_ascii=False, indent=2))
