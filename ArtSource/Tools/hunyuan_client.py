"""腾讯混元生 3D 单 API Key 接口：本机凭据、提交及查询。"""

import argparse
import base64
import ctypes
from ctypes import wintypes
import hashlib
import json
import mimetypes
import os
from pathlib import Path
import re
import sys
import urllib.error
import urllib.parse
import urllib.request


API_ORIGIN = "https://api.ai3d.cloud.tencent.com"
ART_ROOT = Path(__file__).resolve().parents[1]


class ApiError(RuntimeError):
    pass


class SubmissionUnknown(ApiError):
    pass


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, file_pointer, code, message, headers, new_url):
        # 带凭据的请求不跟随重定向，避免把鉴权头转发到其他地址。
        return None


def credential_path():
    return Path(os.environ["LOCALAPPDATA"]) / "GamePJ" / "ArtPipeline" / "hunyuan-api-key.json"


def unprotect_key(encoded_value):
    class DataBlob(ctypes.Structure):
        _fields_ = [("size", wintypes.DWORD), ("data", ctypes.POINTER(ctypes.c_ubyte))]

    raw = base64.b64decode(encoded_value, validate=True)
    buffer = (ctypes.c_ubyte * len(raw)).from_buffer_copy(raw)
    source = DataBlob(len(raw), buffer)
    result = DataBlob()
    crypt = ctypes.WinDLL("crypt32", use_last_error=True)
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    crypt.CryptUnprotectData.argtypes = [
        ctypes.POINTER(DataBlob), ctypes.c_void_p, ctypes.c_void_p,
        ctypes.c_void_p, ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(DataBlob),
    ]
    crypt.CryptUnprotectData.restype = wintypes.BOOL
    kernel.LocalFree.argtypes = [ctypes.c_void_p]
    kernel.LocalFree.restype = ctypes.c_void_p
    if not crypt.CryptUnprotectData(ctypes.byref(source), None, None, None, None, 1, ctypes.byref(result)):
        raise ApiError("无法解密本机密钥，请用当前 Windows 用户重新配置。")
    try:
        return ctypes.string_at(result.data, result.size).decode("utf-8")
    finally:
        kernel.LocalFree(ctypes.cast(result.data, ctypes.c_void_p))


def load_api_key():
    value = os.environ.get("HUNYUAN3D_API_KEY", "").strip()
    if not value:
        path = credential_path()
        if not path.is_file():
            raise ApiError("尚未配置密钥，请运行 Set-HunyuanKey.cmd。")
        record = json.loads(path.read_text(encoding="utf-8-sig"))
        if record.get("api_origin") != API_ORIGIN:
            raise ApiError("凭据的目标服务不匹配。")
        value = unprotect_key(record["protected_key"]).strip()
    if value.startswith("Bearer "):
        value = value[7:].strip()
    if len(value) < 8 or any(character.isspace() for character in value):
        raise ApiError("本机密钥格式不正确，请重新配置。")
    return value


def api_request(operation, payload):
    if operation not in ("submit", "query"):
        raise ApiError("不支持的接口操作。")
    key = load_api_key()
    request = urllib.request.Request(
        f"{API_ORIGIN}/v1/ai3d/{operation}",
        data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
        headers={"Authorization": key, "Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.build_opener(NoRedirect()).open(request, timeout=60) as response:
            data = json.load(response)
    except urllib.error.HTTPError as error:
        # 不回显请求头及完整响应，避免错误页面携带凭据。
        if error.code >= 500:
            raise SubmissionUnknown("混元服务返回临时错误；请先检查控制台，避免重复提交。") from None
        detail = ""
        try:
            body = json.loads(error.read(65536).decode("utf-8"))
            body = body.get("Response", body)
            def safe_error(value, depth=0):
                if depth > 4:
                    return "[nested]"
                if isinstance(value, dict):
                    return {name: safe_error(item, depth + 1) for name, item in value.items()
                            if not any(word in name.lower() for word in ("key", "secret", "authorization", "image", "prompt"))}
                if isinstance(value, list):
                    return [safe_error(item, depth + 1) for item in value[:5]]
                if isinstance(value, str):
                    value = value.replace(key, "[redacted]")
                    return re.sub(r"sk-[A-Za-z0-9_-]+", "[redacted]", value)[:350]
                return value
            detail = json.dumps(safe_error(body), ensure_ascii=False)[:1200]
        except (ValueError, AttributeError):
            pass
        raise ApiError(f"混元接口返回 HTTP {error.code}；{detail or '请检查密钥及服务开通状态。'}") from None
    except (urllib.error.URLError, TimeoutError):
        raise SubmissionUnknown("网络请求未得到确定结果；提交任务时请先检查控制台，避免重复扣费。") from None
    if not isinstance(data, dict):
        raise SubmissionUnknown("混元接口返回了无法识别的数据。")
    result = data.get("Response", data.get("data", data))
    if not isinstance(result, dict):
        raise SubmissionUnknown("混元接口返回结构不符合预期。")
    error = result.get("Error") or result.get("error")
    if error:
        code = error.get("Code", error.get("code", "Unknown")) if isinstance(error, dict) else "Unknown"
        raise ApiError(f"混元接口错误：{str(code).replace(key, '[redacted]')}")
    return result


def image_payload(image_path):
    path = Path(image_path).resolve()
    media_type = mimetypes.guess_type(path.name)[0]
    if media_type not in ("image/png", "image/jpeg", "image/webp"):
        raise ApiError("参考图须为 PNG、JPEG 或 WebP。")
    data = path.read_bytes()
    if len(data) > 6 * 1024 * 1024:
        raise ApiError("参考图超过 6 MiB，请先缩小。")
    # 本机图片使用专业版任务参数中的原始 Base64 字段。
    # 兼容接口示例中的 ImageUrl.Url 对象在本次真实调用中返回 Invalid param。
    return {"ImageBase64": base64.b64encode(data).decode("ascii")}


def submit(args):
    payload = {"Model": args.model, "GenerateType": args.generate_type, "EnablePBR": False}
    if args.generate_type == "LowPoly":
        if args.model != "3.0":
            raise ApiError("文档规定 3.1 不支持 LowPoly，请选择 3.0 或 Normal。")
        payload["PolygonType"] = "quadrilateral"
    if args.image:
        payload.update(image_payload(args.image))
    else:
        payload["Prompt"] = args.prompt
    fingerprint = hashlib.sha256(json.dumps(payload, sort_keys=True).encode("utf-8")).hexdigest()
    job_directory = ART_ROOT / "Generated" / "_jobs"
    job_directory.mkdir(parents=True, exist_ok=True)
    path = job_directory / f"hunyuan-{fingerprint[:20]}.json"
    if path.exists():
        previous = json.loads(path.read_text(encoding="utf-8"))
        if previous.get("job_id"):
            return {"job_id": previous["job_id"], "reused": True, "record": str(path)}
        if previous.get("state") != "rejected":
            raise ApiError("这个请求曾提交但结果未确定；请先核对腾讯控制台，不会自动再次提交。")
    load_api_key()
    record = {"state": "submission_pending", "fingerprint": fingerprint, "model": args.model,
              "generate_type": args.generate_type, "image": str(Path(args.image).resolve()) if args.image else None}
    path.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    try:
        result = api_request("submit", payload)
    except SubmissionUnknown:
        raise
    except ApiError:
        record["state"] = "rejected"
        path.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        raise
    job_id = result.get("JobId") or result.get("job_id")
    if not job_id:
        raise ApiError("服务未返回 JobId；请先检查控制台，不要重复提交。")
    record.update(state="submitted", job_id=str(job_id))
    path.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {"job_id": str(job_id), "reused": False, "record": str(path)}


def download(args):
    if not re.fullmatch(r"[A-Za-z0-9_-]+", args.name):
        raise ApiError("资产名称只能包含英文字母、数字、下划线和短横线。")
    result = api_request("query", {"JobId": args.job_id})
    if result.get("Status") != "DONE":
        raise ApiError(f"任务尚未成功完成：{result.get('Status', 'Unknown')}；{result.get('ErrorCode', '')}")
    directory = ART_ROOT / "Generated" / args.name
    directory.mkdir(parents=True, exist_ok=True)
    manifest_path = directory / "generation.json"
    if manifest_path.exists():
        previous = json.loads(manifest_path.read_text(encoding="utf-8"))
        if previous.get("job_id") != args.job_id:
            raise ApiError("该资产目录已有另一个生成任务，请使用新的资产名称。")
    files = []
    for index, item in enumerate(result.get("ResultFile3Ds", [])):
        url = item.get("Url", "")
        parsed = urllib.parse.urlsplit(url)
        if parsed.scheme != "https" or not parsed.hostname or parsed.username:
            raise ApiError("生成结果返回了不支持的下载地址。")
        extension = Path(parsed.path).suffix.lower()
        if extension not in (".glb", ".zip", ".obj", ".fbx", ".usdz", ".stl"):
            raise ApiError("生成结果返回了不支持的文件格式。")
        kind = re.sub(r"[^A-Za-z0-9_-]", "", item.get("Type", "model")).lower()
        path = directory / f"original-{index:02d}-{kind}{extension}"
        if not path.exists():
            # 文件下载使用独立请求，绝不向下载域名发送 API 鉴权头。
            temporary = path.with_suffix(path.suffix + ".partial")
            with urllib.request.urlopen(url, timeout=90) as response, temporary.open("wb") as target:
                count = 0
                while chunk := response.read(1024 * 1024):
                    count += len(chunk)
                    if count > 512 * 1024 * 1024:
                        raise ApiError("单个模型下载超过 512 MiB，已停止。")
                    target.write(chunk)
            temporary.replace(path)
        content = path.read_bytes()
        if extension == ".glb" and content[:4] != b"glTF":
            raise ApiError("下载文件不是有效 GLB，请检查服务输出。")
        files.append({"type": item.get("Type"), "file": path.name, "bytes": len(content),
                      "sha256": hashlib.sha256(content).hexdigest()})
    if not files:
        raise ApiError("任务成功但没有可下载的模型。")
    manifest = {"job_id": args.job_id, "status": "DONE", "provider": "hunyuan3d",
                "credit_consumed": result.get("ResultCreditConsumed"),
                "credit_details": result.get("ResultCreditDetails"), "files": files}
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {"directory": str(directory), **manifest}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    subparsers.add_parser("status", help="只检查本机凭据能否读取，不请求远程服务")
    query_parser = subparsers.add_parser("query")
    query_parser.add_argument("--job-id", required=True)
    download_parser = subparsers.add_parser("download", help="下载已完成任务，不提交新生成")
    download_parser.add_argument("--job-id", required=True)
    download_parser.add_argument("--name", required=True)
    submit_parser = subparsers.add_parser("submit")
    source = submit_parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--image")
    source.add_argument("--prompt")
    submit_parser.add_argument("--model", choices=("3.0", "3.1"), default="3.0")
    submit_parser.add_argument("--generate-type", choices=("LowPoly", "Normal", "Geometry"), default="LowPoly")
    args = parser.parse_args()
    try:
        if args.command == "status":
            load_api_key()
            result = {"local_key_ready": True, "remote_auth_verified": False, "api_origin": API_ORIGIN}
        elif args.command == "query":
            result = api_request("query", {"JobId": args.job_id})
        elif args.command == "download":
            result = download(args)
        else:
            result = submit(args)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except (ApiError, OSError, ValueError, KeyError) as error:
        print(str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    raise SystemExit(main())
