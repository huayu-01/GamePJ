"""使用虚构数据验证协议和计费防重逻辑，不读真实凭据、不访问网络。"""

import argparse
import base64
import ctypes
from ctypes import wintypes
import io
import json
from pathlib import Path
import tempfile
import unittest
import urllib.error
from unittest.mock import patch

import hunyuan_client as client


class FakeOpener:
    def __init__(self):
        self.requests = []

    def open(self, request, timeout):
        self.requests.append(request)
        return io.BytesIO(json.dumps({"Response": {"JobId": "offline-job"}}).encode())


class HunyuanClientTests(unittest.TestCase):
    def test_dpapi_roundtrip_with_synthetic_data(self):
        class Blob(ctypes.Structure):
            _fields_ = [("size", wintypes.DWORD), ("data", ctypes.POINTER(ctypes.c_ubyte))]

        # 只在内存中加解密测试字符串，不读取或写入真实凭据文件。
        raw = b"synthetic-test-data-not-a-real-key"
        buffer = (ctypes.c_ubyte * len(raw)).from_buffer_copy(raw)
        source = Blob(len(raw), buffer)
        result = Blob()
        crypt = ctypes.WinDLL("crypt32", use_last_error=True)
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        crypt.CryptProtectData.argtypes = [
            ctypes.POINTER(Blob), ctypes.c_void_p, ctypes.c_void_p,
            ctypes.c_void_p, ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(Blob),
        ]
        crypt.CryptProtectData.restype = wintypes.BOOL
        kernel.LocalFree.argtypes = [ctypes.c_void_p]
        kernel.LocalFree.restype = ctypes.c_void_p
        self.assertTrue(crypt.CryptProtectData(ctypes.byref(source), None, None, None, None, 1, ctypes.byref(result)))
        try:
            encoded = base64.b64encode(ctypes.string_at(result.data, result.size)).decode("ascii")
            self.assertEqual(client.unprotect_key(encoded), raw.decode("utf-8"))
        finally:
            kernel.LocalFree(ctypes.cast(result.data, ctypes.c_void_p))

    def test_documented_endpoint_and_plain_authorization(self):
        opener = FakeOpener()
        with patch.object(client, "load_api_key", return_value="synthetic-test-key"), \
                patch.object(client.urllib.request, "build_opener", return_value=opener):
            result = client.api_request("submit", {"Prompt": "offline test"})
        self.assertEqual(result["JobId"], "offline-job")
        request = opener.requests[0]
        self.assertEqual(request.full_url, "https://api.ai3d.cloud.tencent.com/v1/ai3d/submit")
        self.assertEqual(request.get_header("Authorization"), "synthetic-test-key")
        self.assertEqual(request.get_method(), "POST")

    def test_local_image_uses_raw_base64(self):
        with tempfile.TemporaryDirectory() as directory:
            image = Path(directory) / "reference.png"
            image.write_bytes(b"synthetic-image-data")
            result = client.image_payload(image)
        self.assertEqual(base64.b64decode(result["ImageBase64"]), b"synthetic-image-data")

    def test_authentication_error_does_not_echo_key(self):
        failure = urllib.error.HTTPError(
            "https://api.ai3d.cloud.tencent.com/v1/ai3d/query", 401, "Unauthorized", {},
            io.BytesIO(json.dumps({"error": {"code": "invalid_api_key", "message": "Bad key synthetic-test-key"}}).encode()),
        )
        with patch.object(client, "load_api_key", return_value="synthetic-test-key"), \
                patch.object(client.urllib.request, "build_opener") as opener:
            opener.return_value.open.side_effect = failure
            with self.assertRaises(client.ApiError) as caught:
                client.api_request("query", {"JobId": "0"})
        self.assertIn("invalid_api_key", str(caught.exception))
        self.assertNotIn("synthetic-test-key", str(caught.exception))

    def test_duplicate_submit_reuses_existing_job(self):
        with tempfile.TemporaryDirectory() as directory, \
                patch.object(client, "ART_ROOT", Path(directory)), \
                patch.object(client, "load_api_key", return_value="synthetic-test-key"), \
                patch.object(client, "api_request", return_value={"JobId": "offline-job"}) as request:
            args = argparse.Namespace(model="3.0", generate_type="LowPoly", image=None, prompt="offline humanoid")
            self.assertFalse(client.submit(args)["reused"])
            self.assertTrue(client.submit(args)["reused"])
            self.assertEqual(request.call_count, 1)

    def test_unknown_submit_result_does_not_retry(self):
        with tempfile.TemporaryDirectory() as directory, \
                patch.object(client, "ART_ROOT", Path(directory)), \
                patch.object(client, "load_api_key", return_value="synthetic-test-key"), \
                patch.object(client, "api_request", side_effect=client.SubmissionUnknown("offline timeout")) as request:
            args = argparse.Namespace(model="3.0", generate_type="LowPoly", image=None, prompt="offline humanoid")
            with self.assertRaises(client.SubmissionUnknown):
                client.submit(args)
            with self.assertRaises(client.ApiError):
                client.submit(args)
            self.assertEqual(request.call_count, 1)

    def test_unsupported_model_mode_does_not_submit(self):
        args = argparse.Namespace(model="3.1", generate_type="LowPoly", image=None, prompt="offline humanoid")
        with patch.object(client, "api_request") as request, self.assertRaises(client.ApiError):
            client.submit(args)
        request.assert_not_called()

    def test_download_keeps_api_credentials_out_of_file_request(self):
        response = {"Status": "DONE", "ResultFile3Ds": [
            {"Type": "GLB", "Url": "https://example.com/synthetic.glb"}
        ]}
        with tempfile.TemporaryDirectory() as directory, \
                patch.object(client, "ART_ROOT", Path(directory)), \
                patch.object(client, "api_request", return_value=response), \
                patch.object(client.urllib.request, "urlopen", return_value=io.BytesIO(b"glTFsynthetic")) as request:
            result = client.download(argparse.Namespace(name="offline-pilot", job_id="offline-job"))
            self.assertEqual(result["status"], "DONE")
            # 下载只传 URL，不复用携带 Authorization 的 API Request。
            self.assertIsInstance(request.call_args[0][0], str)
            self.assertEqual(request.call_args[0][0], "https://example.com/synthetic.glb")


if __name__ == "__main__":
    unittest.main()
