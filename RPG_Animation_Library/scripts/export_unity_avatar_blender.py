#!/usr/bin/env python3
"""Normalize a target skeleton into the FBX dialect used by animation exports."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import bpy


def parse_args() -> argparse.Namespace:
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-fbx", required=True, type=Path)
    parser.add_argument("--output-fbx", required=True, type=Path)
    parser.add_argument("--output-receipt", required=True, type=Path)
    return parser.parse_args(arguments)


def main() -> int:
    args = parse_args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(args.source_fbx.resolve()), use_anim=False)
    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one target armature, found {len(armatures)}")

    armature = armatures[0]
    armature.animation_data_clear()
    armature.data.pose_position = "REST"
    bpy.ops.object.select_all(action="DESELECT")
    armature.hide_set(False)
    armature.hide_viewport = False
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.context.view_layer.update()

    args.output_fbx.parent.mkdir(parents=True, exist_ok=True)
    result = bpy.ops.export_scene.fbx(
        filepath=str(args.output_fbx.resolve()),
        check_existing=False,
        use_selection=True,
        object_types={"ARMATURE"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=True,
        armature_nodetype="NULL",
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
        batch_mode="OFF",
    )
    if "FINISHED" not in result or not args.output_fbx.is_file():
        raise RuntimeError(f"Avatar FBX export failed: {result}")

    receipt = {
        "schemaVersion": 1,
        "status": "PASS",
        "blenderVersion": bpy.app.version_string,
        "sourceFBX": args.source_fbx.resolve().as_posix(),
        "armature": armature.name,
        "boneCount": len(armature.data.bones),
        "animationOnlyCompatible": True,
    }
    args.output_receipt.parent.mkdir(parents=True, exist_ok=True)
    args.output_receipt.write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"UNITY_AVATAR_EXPORT_OK:{armature.name};bones={receipt['boneCount']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
