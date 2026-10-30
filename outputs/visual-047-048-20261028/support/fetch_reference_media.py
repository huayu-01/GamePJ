from pathlib import Path
import json,re,urllib.request,html,subprocess
import imageio_ffmpeg

d=Path('outputs/visual-047-048-20261028/evidence/references')
def fetch(url):
    return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0'}),timeout=25).read()
sources={}
h=html.unescape((d/'hades.html').read_text(encoding='utf-8')).replace('\\/','/')
urls=list(dict.fromkeys(re.findall(r'https://[^"\s]+/1145350/ss_[a-f0-9]+\.jpg',h)))[:5]
for i,url in enumerate(urls):
    name=f'hades-{i}.jpg'; (d/name).write_bytes(fetch(url));sources[name]=url
for name in ['system_ability','system_linkattack']:
    url=f'https://relink.granbluefantasy.jp/assets/video/en/{name}.mp4'
    (d/(name+'.mp4')).write_bytes(fetch(url));sources[name+'.mp4']=url
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-hide_banner','-loglevel','error','-i',str(d/(name+'.mp4')),'-vf','fps=1/2,scale=960:-1','-frames:v','8',str(d/(name+'-%02d.jpg'))],check=True)
try:
    url='https://www.riotgames.com/en/news/valorant-shaders-and-gameplay-clarity'
    r=fetch(url).decode();(d/'valorant.html').write_text(r,encoding='utf-8')
    imgs=re.findall(r'https[^"<>\s]+shaders(?:16|21)[^"<>\s]*\.(?:png|jpg)[^"<>\s]*',r)
    print('RIOT_IMAGE_URLS',imgs[:8])
    for i,u in enumerate(dict.fromkeys(imgs)):
        try:
            u=html.unescape(u); (d/f'valorant-{i}.png').write_bytes(fetch(u));sources[f'valorant-{i}.png']=u
        except Exception as e: print(e)
except Exception as e: print(e)
(d/'verified-media-sources.json').write_text(json.dumps(sources,indent=2,ensure_ascii=False),encoding='utf-8')
print(json.dumps(sources,ensure_ascii=False))
