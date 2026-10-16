"""只请求作者公开的零价下载流程；不访问付费 upload。暂存目录不入库。"""
import hashlib
import http.cookiejar
import json
import re
import time
import urllib.parse
import urllib.request
from pathlib import Path

root = Path(__file__).resolve().parents[3]
stage = root / '_art_staging' / 'art_refresh'
stage.mkdir(parents=True, exist_ok=True)
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
opener.addheaders = [('User-Agent', 'Mozilla/5.0')]
urls = ['https://binbun3d.itch.io/' + x for x in ['hit-fx', 'muzzle-flash-vfx', 'loot-vfx', 'battle-fx', 'explosion-fx']]
urls.append('https://kyrusgames.itch.io/toon-nature')
for url in urls:
    slug = url.rsplit('/', 1)[-1]
    try:
        page = opener.open(url, timeout=30).read().decode()
        (stage / (slug + '.html')).write_text(page, encoding='utf-8')
        csrf = re.search(r'name="csrf_token" value="([^"]+)"', page).group(1)
        payload = urllib.parse.urlencode({'csrf_token': csrf}).encode()
        req = urllib.request.Request(url + '/download_url', data=payload, headers={'Referer': url, 'X-Requested-With': 'XMLHttpRequest'})
        response = json.loads(opener.open(req, timeout=30).read())
        download_url = response['url']
        downloads = opener.open(download_url, timeout=30).read().decode()
        (stage / (slug + '-downloads.html')).write_text(downloads, encoding='utf-8')
        # 输出仅用于选择公开免费文件，不显示会话令牌或临时下载 URL。
        uploads = re.findall(r'<div class="upload(?: [^"]*)?".*?(?=<div class="upload|$)', downloads, re.S)
        print(slug, 'download page ok', flush=True)
        for upload in uploads:
            names = re.findall(r'<strong[^>]*>(.*?)</strong>', upload, re.S)
            ids = re.findall(r'data-upload_id="(\d+)"', upload)
            print('  ', names[:1], ids[:1], flush=True)
        public_ids = re.findall(r'data-upload_id="(\d+)"', downloads)
        for upload_id in public_ids[:1]:
            token = re.search(r'name="csrf_token" value="([^"]+)"', downloads).group(1)
            file_req = urllib.request.Request(url + '/file/' + upload_id, data=urllib.parse.urlencode({'csrf_token': token}).encode(), headers={'Referer': download_url, 'X-Requested-With': 'XMLHttpRequest'})
            file_info = json.loads(opener.open(file_req, timeout=30).read())
            content = opener.open(file_info['url'], timeout=60).read()
            target = stage / (slug + '.zip')
            target.write_bytes(content)
            print('  SAVED', target.name, len(content), hashlib.sha256(content).hexdigest(), flush=True)
    except Exception as error:
        print(slug, type(error).__name__, str(error), flush=True)
    time.sleep(3)
