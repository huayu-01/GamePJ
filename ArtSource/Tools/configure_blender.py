"""启用美术产线插件，并保留原有 Blender 偏好设置。"""

import json
from pathlib import Path

import addon_utils
import bpy


ART_ROOT = Path(__file__).resolve().parents[1]
REQUIRED_ADDONS = (
    "rigify",
    "node_wrangler",
    "bl_ext.user_default.retarget",
    "blender_mcp",
)


def configure():
    # 先全部启用成功，再保存偏好设置。
    for module_name in REQUIRED_ADDONS:
        if module_name not in bpy.context.preferences.addons:
            bpy.ops.preferences.addon_enable(module=module_name)
        if not addon_utils.check(module_name)[1]:
            raise RuntimeError(f"插件未能启用：{module_name}")

    bpy.context.preferences.addons["blender_mcp"].preferences.telemetry_consent = False

    library_path = str(ART_ROOT / "Blender")
    libraries = bpy.context.preferences.filepaths.asset_libraries
    if not any(Path(item.path) == Path(library_path) for item in libraries):
        bpy.ops.preferences.asset_library_add(directory=library_path)
        libraries[-1].name = "GamePJ ArtSource"

    bpy.ops.wm.save_userpref()
    report = {
        "blender_version": bpy.app.version_string,
        "enabled_addons": list(REQUIRED_ADDONS),
        "asset_library": library_path,
        "telemetry_consent": False,
        "rigify_operator": bpy.ops.object.armature_human_metarig_add.idname(),
        "gltf_export_operator": bpy.ops.export_scene.gltf.idname(),
        "gltf_import_operator": bpy.ops.import_scene.gltf.idname(),
    }
    report_path = ART_ROOT / "Tools" / "blender-environment.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("ART_PIPELINE_CONFIG=" + json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    configure()
