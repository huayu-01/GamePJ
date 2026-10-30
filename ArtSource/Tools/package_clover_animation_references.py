# -*- coding: utf-8 -*-
"""将真实人形源动作帧整理为原速视频、慢放GIF和可浏览的动作参考册。"""
import html
import json
import math
import subprocess
from pathlib import Path
import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont

ROOT = Path('F:/Godot项目/项目a/ArtSource/Animations/References/clover-wayfarer-basics')
MEDIA = ROOT/'media'
MANIFEST = json.loads((ROOT/'render-manifest.json').read_text(encoding='utf-8'))
CLIPS = sorted(MANIFEST['clips'], key=lambda c:c['id'])
assert len(CLIPS)==12, '动画参考尚未渲染完成'
FPS = MANIFEST['fps']
WIDTH, HEIGHT = 480, 608
FONT = 'C:/Windows/Fonts/msyh.ttc'
TITLE_FONT = ImageFont.truetype(FONT, 25)
BODY_FONT = ImageFont.truetype(FONT, 16)
SMALL_FONT = ImageFont.truetype(FONT, 13)
FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()


def wrapped(text, font, width):
    lines=[]; line=''
    for char in text:
        if font.getlength(line+char)>width:
            lines.append(line); line=char
        else: line+=char
    if line: lines.append(line)
    return lines


def annotate(frame, clip, slow=False):
    image=Image.new('RGB',(WIDTH,HEIGHT),'#202c2f')
    image.paste(frame,(0,56))
    draw=ImageDraw.Draw(image)
    draw.text((16,7),clip['id'][:2]+'  '+clip['label'],font=TITLE_FONT,fill='#f3f6ef')
    draw.text((190,15),clip['source_clip'],font=SMALL_FONT,fill='#a9cec3')
    mode='0.5× 慢放参考' if slow else '原速源动作'
    library='UAL 2' if 'UAL2' in clip['source_file'] else 'UAL 1'
    draw.text((16,542),f'{mode}  ·  {library}  ·  {clip["source_duration_seconds"]:.2f}s',font=SMALL_FONT,fill='#a9cec3')
    for i,line in enumerate(wrapped(clip['note'],BODY_FONT,WIDTH-32)[:2]):
        draw.text((16,562+i*20),line,font=BODY_FONT,fill='#e3ebe5')
    return image


def encoder(path):
    command=[FFMPEG,'-y','-loglevel','error','-f','rawvideo','-vcodec','rawvideo','-s',f'{WIDTH}x{HEIGHT}','-pix_fmt','rgb24','-r',str(FPS),'-i','-','-an','-c:v','libx264','-preset','veryfast','-crf','21','-pix_fmt','yuv420p','-movflags','+faststart',str(path)]
    return subprocess.Popen(command,stdin=subprocess.PIPE,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)


def finish(process):
    process.stdin.close()
    error=process.stderr.read().decode('utf-8',errors='replace')
    result=process.wait()
    assert result==0,error


MEDIA.mkdir(exist_ok=True)
reel=encoder(ROOT/'basic-motion-reference-reel.mp4')
reel_time=0
posters=[]
for clip in CLIPS:
    files=sorted((ROOT/'frames'/clip['id']).glob('*.png'))
    assert len(files)==clip['frame_count'],clip['id']
    frames=[]
    for path in files:
        with Image.open(path) as image: frames.append(image.convert('RGB'))
    annotated=[annotate(frame,clip) for frame in frames]
    loop=clip['source_clip'].endswith('Loop')
    if loop: sequence=annotated*2
    else: sequence=[annotated[0]]*round(FPS*.25)+annotated+[annotated[-1]]*round(FPS*.45)
    video=encoder(MEDIA/(clip['id']+'.mp4'))
    clip['reel_start_seconds']=round(reel_time,3)
    clip['preview_duration_seconds']=len(sequence)/FPS
    for frame in sequence:
        raw=frame.tobytes()
        video.stdin.write(raw)
        reel.stdin.write(raw)
    finish(video)
    reel_time+=len(sequence)/FPS
    slow=[annotate(frame,clip,True) for frame in frames]
    if not loop: slow=[slow[0]]*4+slow+[slow[-1]]*8
    palette=Image.Quantize.FASTOCTREE
    quantized=[frame.quantize(colors=160,method=palette) for frame in slow]
    quantized[0].save(MEDIA/(clip['id']+'.gif'),save_all=True,append_images=quantized[1:],duration=round(2000/FPS),loop=0,disposal=2,optimize=False)
    fraction=.70 if clip['id'].startswith(('05','08','09','10')) else .25
    poster=annotated[min(len(annotated)-1,round((len(annotated)-1)*fraction))]
    poster.save(MEDIA/(clip['id']+'.png'))
    posters.append(poster)
    print('PACKAGED '+clip['id'],flush=True)
finish(reel)
sheet=Image.new('RGB',(1280,1218),'#202c2f')
for i,poster in enumerate(posters):
    tile=poster.resize((320,405),Image.Resampling.LANCZOS)
    sheet.paste(tile,((i%4)*320,(i//4)*406))
sheet.save(ROOT/'motion-reference-overview.jpg',quality=93)

sources=[
    {'name':'KayKit Character Animations','page':'https://kaylousberg.itch.io/kaykit-character-animations','video':'https://www.youtube.com/watch?v=zSzzEmdIiXI','role':'偏卡通的动作风格参考；优先观察短身形角色的步态与重心。','license':'作者页标注CC0','local_render':False},
    {'name':'Quaternius Universal Animation Library 1','page':'https://quaternius.itch.io/universal-animation-library','video':'https://www.youtube.com/watch?v=-VXFlXhvD6A','role':'本批11段源动作；本地Standard包有43个条目（含参考姿势）。','license':'本地许可与作者页均标注CC0','local_render':True},
    {'name':'Quaternius Universal Animation Library 2','page':'https://quaternius.itch.io/universal-animation-library-2','video':'https://www.youtube.com/watch?v=2Hd5nH122OE','role':'本批Hit_Knockback源动作；本地Standard包有43个条目。','license':'本地许可与作者页均标注CC0','local_render':True},
    {'name':'ORICUT Forest Spirit Nori','page':'https://oricutstudios.itch.io/low-poly-forest-spirit-nori','video':'https://www.youtube.com/watch?v=nb_wj9aTnOM','role':'最接近当前角色比例与斗篷的风格参照；只引用作者公开展示。','license':'付费角色资产；本次未获取或使用其模型与动作文件','local_render':False},
]
(ROOT/'sources.json').write_text(json.dumps({'review_date':'2026-10-25','sources':sources},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(ROOT/'selection.json').write_text(json.dumps({'render_fps':FPS,'gif_speed':.5,'reel_duration_seconds':reel_time,'source_motion':True,'retargeted_to_clover_wayfarer':False,'hands_allowed_in_references':True,'clips':CLIPS},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

cards=[]
for clip in CLIPS:
    identifier=clip['id']
    cards.append(f'''<article><h2>{identifier[:2]} · {html.escape(clip['label'])}</h2><video controls loop muted playsinline preload="none" poster="media/{identifier}.png"><source src="media/{identifier}.mp4" type="video/mp4"></video><p class="code">{html.escape(clip['source_clip'])} · {clip['source_duration_seconds']:.2f}s</p><p>{html.escape(clip['note'])}</p><div><a href="media/{identifier}.gif">0.5× GIF慢放</a><a href="media/{identifier}.mp4">原速MP4</a></div></article>''')
source_links=''.join(f'<li><a href="{s["page"]}">{html.escape(s["name"])}</a> · <a href="{s["video"]}">作者视频</a> — {html.escape(s["role"])}</li>' for s in sources)
document='''<!doctype html>
<html lang="zh-CN">
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>三叶旅童 · 基础动作参考</title>
<style>*{box-sizing:border-box}body{margin:0;background:#172327;color:#e8efeb;font:16px/1.6 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1280px;margin:auto;padding:32px}h1{margin:0 0 12px;font-size:32px}h2{margin:0 0 12px;font-size:21px}p{margin:8px 0}a{color:#91dfbe}.intro{max-width:900px;color:#c5d5ce}.sources{padding:20px 24px;background:#233337;border-radius:12px;margin:24px 0}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(285px,1fr));gap:20px}article{background:#233337;border:1px solid #3b514e;border-radius:12px;padding:16px}video{display:block;width:100%;background:#202c2f;border-radius:6px}.code{font:13px/1.5 monospace;color:#9ecdbb}article div{display:flex;gap:16px;margin-top:16px}footer{margin-top:28px;color:#b6c9bf}</style>
<main>
<h1>三叶旅童 · 基础动作参考</h1>
<p class="intro">首批12段完整人形源动作。重点看躯干重心、髋部、腿脚落点与节奏，手部动作不作为排除条件。当前角色需要缩小步幅、调整短腿接触，并另做斗篷的拖曳回摆。这些是参考媒体，尚未重定向到三叶旅童。</p>
<p>
<a href="basic-motion-reference-reel.mp4">播放约30秒原速合集</a> · <a href="motion-reference-overview.jpg">动作总览图</a>
</p>
<section class="sources">
<strong>风格和动作来源</strong>
<ul>'''+source_links+'''</ul>
<p>本地预览来自已有的Quaternius Standard动画库；循环动作重复两轮，一次性动作首尾有短暂停顿，动作阶段保持原速。GIF用于0.5×慢放观察。</p>
</section>
<section class="grid">'''+''.join(cards)+'''</section>
<footer>尚待补充：独立下落、转向、急停。Jump_Loop仅作为空中姿态参考，不冒充已找到专用下落片段。原始动作文件与当前角色均未修改。</footer>
</main>
</html>'''
(ROOT/'index.html').write_text(document,encoding='utf-8')
print('REFERENCE_PACKAGE_DONE '+json.dumps({'clips':len(CLIPS),'reel_seconds':reel_time,'media_files':len(list(MEDIA.iterdir()))}),flush=True)
