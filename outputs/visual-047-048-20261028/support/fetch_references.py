from pathlib import Path
import json, re, urllib.request

def fetch(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent':'Mozilla/5.0'}), timeout=20).read()

destination = Path('outputs/visual-047-048-20261028/evidence/references')
destination.mkdir(exist_ok=True)
sources = {
    'valorant': 'https://www.riotgames.com/darkroom/original/730619a5cd2a5a8633e90610cf94391e%3Abf2ebcc24cb902ebfde321562776b258/shaders16.png',
    'valorant-depth': 'https://www.riotgames.com/darkroom/original/ff15c87635a26dfcf2eb9d773cfd99ef%3A94684429574231e6f4902ae2b62be0f5/shaders21.png'
}
for name, url in sources.items():
    try:
        (destination / (name + '.png')).write_bytes(fetch(url))
    except Exception as error:
        print(name, str(error))
for name, url in [('relink','https://relink.granbluefantasy.jp/en/gameplay'),('hades','https://store.steampowered.com/app/1145350/Hades_II/')]:
    try:
        html = fetch(url).decode('utf-8')
        (destination / (name+'.html')).write_text(html, encoding='utf-8')
        if name == 'hades':
            urls = list(dict.fromkeys(re.findall(r'https://[^"\s]+/steam/apps/1145350/ss_[^"\s?]+', html)))[:8]
        else:
            urls = re.findall(r'(?:src|poster)=["\x27]([^"\x27]+)',html)
        print(name, json.dumps(urls,ensure_ascii=False))
    except Exception as error:
        print(name, str(error))
(destination/'sources.json').write_text(json.dumps(sources,indent=2),encoding='utf-8')
