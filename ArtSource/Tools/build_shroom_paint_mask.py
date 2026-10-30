"""从现有 GLB 的静态位置和原贴图生成上色区域，不改动模型。"""
import hashlib
import io
import json
from pathlib import Path
import struct

import numpy as np
from PIL import Image, ImageFilter


PROJECT = Path(__file__).resolve().parents[2]
SOURCE = PROJECT / "Assets/Enemies/ScaredyShroom/scaredy_shroom.glb"
TARGET = PROJECT / "Assets/Materials/EnemyAnime/shroom_regions.png"


def read_glb(path):
    data = path.read_bytes()
    magic, version, length = struct.unpack_from("<III", data)
    if (magic, version, length) != (0x46546C67, 2, len(data)):
        raise ValueError("需要完整的 glTF 2.0 二进制文件")
    chunks = {}
    offset = 12
    while offset < len(data):
        size, kind = struct.unpack_from("<II", data, offset)
        chunks[kind] = data[offset + 8:offset + 8 + size]
        offset += 8 + size
    return json.loads(chunks[0x4E4F534A]), chunks[0x004E4942], data


def accessor(document, binary, index):
    entry = document["accessors"][index]
    view = document["bufferViews"][entry["bufferView"]]
    dtype = np.dtype({5126: "<f4", 5123: "<u2", 5125: "<u4"}[entry["componentType"]])
    width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[entry["type"]]
    return np.ndarray(
        (entry["count"], width), dtype=dtype, buffer=binary,
        offset=view.get("byteOffset", 0) + entry.get("byteOffset", 0),
        strides=(view.get("byteStride", dtype.itemsize * width), dtype.itemsize),
    )


def bake_positions(positions, uvs, faces, width, height):
    # 在 UV 三角形中插值静态坐标，避免动画后的位置改变材质分类。
    result = np.zeros((height, width, 3), dtype=np.float32)
    covered = np.zeros((height, width), dtype=bool)
    pixels = uvs * np.array([width, height]) - 0.5
    for face in faces:
        triangle = pixels[face]
        low = np.maximum(np.floor(triangle.min(axis=0)).astype(int), 0)
        high = np.minimum(np.ceil(triangle.max(axis=0)).astype(int), [width - 1, height - 1])
        if np.any(high < low):
            continue
        a, b, c = triangle
        denominator = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(denominator) < 1e-8:
            continue
        yy, xx = np.mgrid[low[1]:high[1] + 1, low[0]:high[0] + 1]
        first = ((b[1] - c[1]) * (xx - c[0]) + (c[0] - b[0]) * (yy - c[1])) / denominator
        second = ((c[1] - a[1]) * (xx - c[0]) + (a[0] - c[0]) * (yy - c[1])) / denominator
        third = 1.0 - first - second
        inside = (first >= -1e-5) & (second >= -1e-5) & (third >= -1e-5)
        interpolated = (first[..., None] * positions[face[0]]
                        + second[..., None] * positions[face[1]]
                        + third[..., None] * positions[face[2]])
        region = np.s_[low[1]:high[1] + 1, low[0]:high[0] + 1]
        result[region][inside] = interpolated[inside]
        covered[region] |= inside
    return result, covered


def build():
    document, binary, original = read_glb(SOURCE)
    material = next(i for i, item in enumerate(document["materials"])
                    if item["name"].startswith("ShroomGeneratedSurface"))
    primitive = next(primitive for mesh in document["meshes"] for primitive in mesh["primitives"]
                     if primitive.get("material") == material)
    attributes = primitive["attributes"]
    positions = accessor(document, binary, attributes["POSITION"])
    uvs = accessor(document, binary, attributes["TEXCOORD_0"])
    faces = accessor(document, binary, primitive["indices"]).reshape(-1, 3)
    texture_index = document["materials"][material]["pbrMetallicRoughness"]["baseColorTexture"]["index"]
    image_index = document["textures"][texture_index]["source"]
    view = document["bufferViews"][document["images"][image_index]["bufferView"]]
    offset = view.get("byteOffset", 0)
    image = Image.open(io.BytesIO(binary[offset:offset + view["byteLength"]])).convert("RGB")
    position_map, covered = bake_positions(positions, uvs, faces, *image.size)
    # 原图只辅助定位既有面部标记；位置边界负责排除菌柄底部和菌盖内侧。
    source = np.asarray(image, dtype=np.float32) / 255.0
    maximum = source.max(axis=2)
    chroma = (maximum - source.min(axis=2)) / np.maximum(maximum, 0.001)
    luminance = source @ np.array([0.2126, 0.7152, 0.0722])
    x, y, z = np.moveaxis(position_map, 2, 0)
    purple = (source[..., 0] > source[..., 1] * 1.10) & (source[..., 2] > source[..., 1] * 1.18)
    purple &= source[..., 2] > source[..., 0] * 0.70
    cap = covered & (purple | (y > 1.105) | ((y > 0.86) & (x * x + z * z > 0.275 ** 2)))
    face = covered & ~cap & (np.abs(x) < 0.16) & (z > 0.13)
    eye_area = face & (y > 0.80) & (y < 0.995) & (z < 0.245)
    eye = eye_area & (chroma < 0.40) & (luminance > 0.22)
    dark = face & (y > 0.65) & (y < 1.01) & (luminance < 0.16)
    # 闭运算消除眼白中的细小杂色；外扩只填充 UV 岛边缘的采样空隙。
    eye_image = Image.fromarray(eye.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))
    eye = (np.asarray(eye_image) > 127) & eye_area
    channels = []
    for region in (cap, eye, dark):
        channel = np.asarray(Image.fromarray(region.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(5))).copy()
        channel[covered] = region[covered].astype(np.uint8) * 255
        channels.append(channel)
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.stack(channels, axis=2)).save(TARGET)
    print(json.dumps({"output": str(TARGET), "source_sha256": hashlib.sha256(original).hexdigest(),
                      "size": image.size, "cap_pixels": int(cap.sum()), "eye_pixels": int(eye.sum()),
                      "dark_pixels": int(dark.sum())}, ensure_ascii=False))


if __name__ == "__main__":
    build()
