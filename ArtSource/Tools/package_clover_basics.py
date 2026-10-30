"""打包角色本体六项动作预览，原速视频、慢放和同屏GIF。"""
import json
import math
import html
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import imageio_ffmpeg

ROOT=Path('F:/Godot项目/项目a/ArtSource/Animations/clover-wayfarer/basic-trial')
manifest=json.loads((ROOT/'motion-manifest.json').read_text(encoding='utf-8'))
MEDIA=ROOT/'media';MEDIA.mkdir(exist_ok=True)
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
SMALL=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
FFMPEG=imageio_ffmpeg.get_ffmpeg_exe()
FPS=24
SIZE=(768,672)
notes={
 'idle':'轻微呼吸与重心变化',
 'walk':'短步幅，足底接触校正',
 'sprint':'快跑参考，减弱躯干摆动与起伏',
 'hit':'站立受击，后移留给运行位移',
 'die':'失衡倒地，保持最后姿态',
 'jump':'短促起跳；展示升降不烘焙进动作',
}


def annotated(image,clip):
    canvas=Image.new('RGB',SIZE,'#1a2927')
    canvas.paste(image,(0,48))
    draw=ImageDraw.Draw(canvas)
    draw.text((18,8),clip['label']+' / '+clip['name'],font=FONT,fill='#f3f6ea')
    draw.text((490,12),'原速 · 正斜面 / 侧面',font=SMALL,fill='#9fd2b8')
    draw.text((18,635),notes[clip['name']],font=SMALL,fill='#e0eadc')
    return canvas


def encoder(path):
    command=[FFMPEG,'-y','-loglevel','error','-f','rawvideo','-vcodec','rawvideo',
     '-s',f'{SIZE[0]}x{SIZE[1]}','-pix_fmt','rgb24','-r',str(FPS),'-i','-',
     '-an','-c:v','libx264','-preset','veryfast','-crf','20','-pix_fmt','yuv420p',
     '-movflags','+faststart',str(path)]
    return subprocess.Popen(command,stdin=subprocess.PIPE,stderr=subprocess.PIPE)


def finish(process):
    process.stdin.close()
    error=process.stderr.read().decode('utf-8',errors='replace')
    assert process.wait()==0,error


reel=encoder(ROOT/'clover-basic-six.mp4')
all_frames={}
cards=[]
for clip in manifest['clips']:
    name=clip['name']
    paths=sorted((ROOT/'frames'/name).glob('*.png'))
    assert len(paths)==clip['render_frame_count'],name
    frames=[annotated(Image.open(p).convert('RGB'),clip) for p in paths]
    all_frames[name]=frames
    sequence=frames*2 if clip['loop'] else [frames[0]]*6+frames+[frames[-1]]*12
    video=encoder(MEDIA/(name+'.mp4'))
    for frame in sequence:
        raw=frame.tobytes();video.stdin.write(raw);reel.stdin.write(raw)
    finish(video)
    slow=[f.resize((576,504),Image.Resampling.LANCZOS).quantize(colors=128,method=Image.Quantize.FASTOCTREE) for f in sequence]
    slow[0].save(MEDIA/(name+'.gif'),save_all=True,append_images=slow[1:],duration=83,loop=0,disposal=2,optimize=False)
    frames[len(frames)//2].save(MEDIA/(name+'.jpg'),quality=92)
    cards.append(f'''<article><h2>{html.escape(clip['label'])}</h2>
<video controls muted playsinline loop preload="metadata" poster="media/{name}.jpg">
<source src="media/{name}.mp4" type="video/mp4"></video>
<p>{html.escape(notes[name])}</p><p class="meta">{clip['duration_seconds']:.2f}s · {'循环' if clip['loop'] else '单次'} · 骨骼烘焙60fps</p>
<button onclick="this.parentNode.querySelector('video').playbackRate=1">原速</button>
<button onclick="this.parentNode.querySelector('video').playbackRate=.5">半速</button>
<a href="media/{name}.gif">慢放GIF</a></article>''')
finish(reel)
mosaic=[]
for i in range(72):
    canvas=Image.new('RGB',(960,1260),'#1a2927')
    for j,clip in enumerate(manifest['clips']):
        frames=all_frames[clip['name']]
        k=i%len(frames) if clip['loop'] else min(max(0,i-4),len(frames)-1)
        tile=frames[k].resize((480,420),Image.Resampling.LANCZOS)
        canvas.paste(tile,((j%2)*480,(j//2)*420))
    mosaic.append(canvas.quantize(colors=160,method=Image.Quantize.FASTOCTREE))
mosaic[0].save(ROOT/'six-motion-preview.gif',save_all=True,append_images=mosaic[1:],duration=[42,42,41]*24,loop=0,disposal=2,optimize=False)
document='''<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>三叶旅童 · 六项基础动作试绑</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#14221f;color:#eff4e8;font:16px/1.65 system-ui,"Microsoft YaHei",sans-serif}
main{max-width:1360px;margin:auto;padding:28px}h1{margin:0 0 10px}h2{margin:0 0 10px}
p{margin:8px 0}.intro{max-width:1000px;color:#c4d6c8}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(350px,1fr));gap:20px;margin-top:24px}
article{border:1px solid #3c5749;border-radius:12px;background:#20352c;padding:18px}video{display:block;width:100%;border-radius:7px}
a{color:#a7dfb5}button{margin:8px 10px 0 0;border:1px solid #658e73;background:#284e3a;color:white;padding:8px 16px;border-radius:6px;cursor:pointer}.meta{font-size:13px;color:#a6c2ad}
</style><main><h1>三叶旅童 · 六项基础动作试绑</h1>
<p class="intro">同一份当前角色的实际骨骼动画，正斜面与侧面同步播放。待机、走、跑采用循环；受击、死亡、跳跃是单次动作，视频首尾留有观察停顿。点击半速可逐段检查。</p>
<p class="intro">已排除慢跑与击倒式击退。跳跃去掉长蓄力，预览的升降轨迹由展示层添加，模型动作只保存姿态；正式游戏高度交给移动与重力。受击不包含后退距离，之后可配合击退位移。</p>
<p><a href="clover-basic-six.mp4">播放六项原速合集</a> · <a href="six-motion-preview.gif">六项同屏预览</a> · <a href="motion-manifest.json">动作数据</a></p>
<section class="grid">'''+''.join(cards)+'''</section>
<p class="intro">当前为美术动作试绑候选，尚未替换游戏生产角色。斗篷使用三根辅助骨的跟随动画，尚未做实时布料碰撞。</p>
</main></html>'''
(ROOT/'index.html').write_text(document,encoding='utf-8')
print('CW_PACKAGE_DONE',flush=True)
