"""对独立 Blender 验证实例执行真实 MCP 和 GLB 检查。"""

import asyncio
import json
import os
from pathlib import Path

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client


TOOLS_DIR = Path(__file__).resolve().parent
USER_PROMPT = (
    "ok,那我先搞blander,我已经下载好了,E:\\steam\\steamapps\\common\\Blender路径,"
    "后面的素材文件则在当前的项目里建立;\n"
    "现在是需要先从社区里装入一些必须的插件,包括mcp;"
)


async def verify():
    environment = dict(os.environ)
    environment.update({
        "BLENDER_HOST": "127.0.0.1",
        "BLENDER_PORT": "9877",
        "DISABLE_TELEMETRY": "true",
        "BLENDER_MCP_SAFE_MODE": "1",
    })
    parameters = StdioServerParameters(
        command="C:/Users/huayu/.local/bin/mcp-for-blender.exe",
        env=environment,
    )
    report = {}
    async with stdio_client(parameters) as (reader, writer):
        async with ClientSession(reader, writer) as session:
            initialization = await session.initialize()
            report["server"] = initialization.serverInfo.model_dump()
            tool_list = await session.list_tools()
            report["tools"] = [tool.name for tool in tool_list.tools]
            for tool_name, arguments in (
                ("get_addon_status", {}),
                ("get_scene_info", {"limit": 3}),
            ):
                arguments["user_prompt"] = USER_PROMPT
                result = await session.call_tool(tool_name, arguments)
                if result.isError:
                    raise RuntimeError(f"MCP 调用失败：{tool_name}：{result}")
                report[tool_name] = result.model_dump(mode="json", exclude_none=True)
                print(f"MCP_CALL_OK={tool_name}", flush=True)

            glb_path = str(TOOLS_DIR / "cache" / "installation-smoke.glb")
            # 仅在独立验证实例中创建临时物体，不写入正式资产目录。
            code = f"""import bpy
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.mesh.primitive_cube_add(size=1)
source = bpy.context.object
source.name = 'ArtPipelineInstallationSmoke'
bpy.ops.export_scene.gltf(filepath={glb_path!r}, export_format='GLB', use_selection=True)
bpy.data.objects.remove(source, do_unlink=True)
bpy.ops.import_scene.gltf(filepath={glb_path!r})
imported = bpy.context.selected_objects[0]
assert abs(imported.dimensions.z - 1.0) < 0.0001
bpy.data.objects.remove(imported, do_unlink=True)
bpy.ops.object.armature_human_metarig_add()
metarig = bpy.context.object
assert len(metarig.data.bones) > 20
bpy.ops.pose.rigify_generate()
assert len(bpy.context.object.data.bones) > 100
assert 'bl_ext.user_default.retarget' in bpy.context.preferences.addons
assert 'node_wrangler' in bpy.context.preferences.addons
print('PIPELINE_SMOKE_OK', bpy.app.version_string, len(bpy.context.object.data.bones))
"""
            result = await session.call_tool(
                "execute_blender_code", {"code": code, "user_prompt": USER_PROMPT}
            )
            report["smoke"] = result.model_dump(mode="json", exclude_none=True)
            text_result = "\n".join(getattr(item, "text", "") for item in result.content)
            if result.isError or "PIPELINE_SMOKE_OK" not in text_result:
                raise RuntimeError(f"Blender 功能验证失败：{text_result}")
            print(text_result, flush=True)

    report["passed"] = True
    (TOOLS_DIR / "blender-validation.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print("MCP_VALIDATION_PASSED", flush=True)


if __name__ == "__main__":
    asyncio.run(verify())
