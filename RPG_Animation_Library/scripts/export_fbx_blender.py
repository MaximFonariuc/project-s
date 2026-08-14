#!/usr/bin/env python3
"""Export one Phase 7 target action as an animation-only Unity FBX."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import bpy


def parse_args() -> argparse.Namespace:
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--retarget-metadata", required=True, type=Path)
    parser.add_argument("--output-fbx", required=True, type=Path)
    parser.add_argument("--output-receipt", required=True, type=Path)
    return parser.parse_args(arguments)


def main() -> int:
    args = parse_args()
    expected = json.loads(args.retarget_metadata.read_text(encoding="utf-8"))
    armature = bpy.data.objects.get(expected["targetArmatureName"])
    if armature is None or armature.type != "ARMATURE":
        raise RuntimeError("Retargeted target armature was not found")
    if armature.animation_data is None or armature.animation_data.action is None:
        raise RuntimeError("Retargeted target action was not found")

    bpy.ops.object.select_all(action="DESELECT")
    armature.hide_set(False)
    armature.hide_viewport = False
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    armature.data.pose_position = "POSE"
    source_frame_start = int(expected["frameStart"])
    source_frame_end = int(expected["frameEnd"])
    frame_count = source_frame_end - source_frame_start + 1
    bind_pose_frame = 0
    frame_start = 1
    frame_end = frame_count
    bpy.context.scene.frame_start = bind_pose_frame
    bpy.context.scene.frame_end = frame_end
    bpy.context.scene.render.fps = int(expected["frameRate"])
    action = armature.animation_data.action

    # Keep rest/bind evidence separate from the authored motion. Unity excludes
    # this prefix from the clip and uses the normalized Avatar from the contract.
    track = armature.animation_data.nla_tracks.new()
    track.name = "__UNITY_BIND_POSE_PREFIX__"
    strip = track.strips.new(action.name, frame_start, action)
    strip.action_frame_start = source_frame_start
    strip.action_frame_end = source_frame_end
    strip.frame_start = frame_start
    strip.frame_end = frame_end
    strip.extrapolation = "NOTHING"
    strip.blend_type = "REPLACE"
    strip.influence = 1.0
    armature.animation_data.action = None
    for pose_bone in armature.pose.bones:
        pose_bone.matrix_basis.identity()
    bpy.context.scene.frame_set(bind_pose_frame)
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
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="AUTO",
        embed_textures=False,
        batch_mode="OFF",
    )
    if "FINISHED" not in result or not args.output_fbx.is_file():
        raise RuntimeError(f"FBX export failed: {result}")

    receipt = {
        "schemaVersion": 1,
        "status": "PASS",
        "animationId": expected["animationId"],
        "blenderVersion": bpy.app.version_string,
        "armature": armature.name,
        "action": action.name,
        "frameStart": frame_start,
        "frameEnd": bpy.context.scene.frame_end,
        "frameCount": frame_count,
        "bindPoseFrame": bind_pose_frame,
        "exportFrameStart": bpy.context.scene.frame_start,
        "exportFrameCount": bpy.context.scene.frame_end - bpy.context.scene.frame_start + 1,
        "frameRate": bpy.context.scene.render.fps,
        "settings": {
            "objectTypes": ["ARMATURE"],
            "restPosePrefix": True,
            "axisForward": "-Z",
            "axisUp": "Y",
            "addLeafBones": False,
            "deformBonesOnly": True,
            "bakeStep": 1.0,
            "simplify": 0.0,
        },
    }
    args.output_receipt.parent.mkdir(parents=True, exist_ok=True)
    args.output_receipt.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"FBX_EXPORT_OK:{expected['animationId']};frames={receipt['frameCount']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
