"""把测试剑的 glTF、二进制和原始贴图合并为便于移动的 GLB，不改贴图像素。"""
from pathlib import Path
import json
import struct

project = Path(__file__).resolve().parents[3]
folder = project / "Assets/Weapons/KayKit"
source = folder / "Models"
document = json.loads((source / "sword_A.gltf").read_text())
binary = bytearray((source / document["buffers"][0]["uri"]).read_bytes())
for texture in document.get("images", []):
    binary.extend(b"\0" * (-len(binary) % 4))
    content = (source / texture.pop("uri")).read_bytes()
    texture["bufferView"] = len(document["bufferViews"])
    document["bufferViews"].append({"buffer": 0, "byteOffset": len(binary), "byteLength": len(content)})
    texture["mimeType"] = "image/png"
    binary.extend(content)
document["buffers"] = [{"byteLength": len(binary)}]
header = json.dumps(document, separators=(",", ":")).encode()
header += b" " * (-len(header) % 4)
binary.extend(b"\0" * (-len(binary) % 4))
output = struct.pack("<4sII", b"glTF", 2, 28 + len(header) + len(binary))
output += struct.pack("<I4s", len(header), b"JSON") + header
output += struct.pack("<I4s", len(binary), b"BIN\0") + binary
(folder / "pilot_sword.glb").write_bytes(output)
