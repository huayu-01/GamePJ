"""启动时检查插件；可通过命令行指定独立验证端口。"""

import os
import sys
from pathlib import Path

os.environ["DISABLE_TELEMETRY"] = "true"

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from configure_blender import configure


configure()
arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if arguments:
    bpy.context.scene.blendermcp_port = int(arguments[0])
bpy.context.scene.blendermcp_auto_start_server = True
print(f"ART_PIPELINE_START_PORT={bpy.context.scene.blendermcp_port}", flush=True)
