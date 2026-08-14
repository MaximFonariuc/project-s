#!/usr/bin/env python3
"""Import one Elden Ring HKX animation and save a native Blender pilot artifact."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import sys
import traceback
from pathlib import Path

import bpy
import gpu


BODY_BONES = {
    "RootRotY",
    "Pelvis",
    "Spine",
    "Spine1",
    "Spine2",
    "Neck",
    "Head",
    "L_Clavicle",
    "L_UpperArm",
    "L_Elbow",
    "L_Forearm",
    "L_Hand",
    "R_Clavicle",
    "R_UpperArm",
    "R_Elbow",
    "R_Forearm",
    "R_Hand",
    "L_Thigh",
    "L_Knee",
    "L_Calf",
    "L_Foot",
    "L_Toe0",
    "R_Thigh",
    "R_Knee",
    "R_Calf",
    "R_Foot",
    "R_Toe0",
}


class PipelineLog:
    @staticmethod
    def settings(context):
        return context.scene.soulstruct_settings

    @staticmethod
    def deselect_all():
        if bpy.ops.object.select_all.poll():
            bpy.ops.object.select_all(action="DESELECT")

    @staticmethod
    def to_object_mode(context):
        if context.mode != "OBJECT" and bpy.ops.object.mode_set.poll():
            bpy.ops.object.mode_set(mode="OBJECT", toggle=False)

    @staticmethod
    def to_edit_mode(context):
        if not context.mode.startswith("EDIT") and bpy.ops.object.mode_set.poll():
            bpy.ops.object.mode_set(mode="EDIT", toggle=False)

    @staticmethod
    def debug(message):
        print(f"DEBUG: {message}")

    @staticmethod
    def info(message):
        print(f"INFO: {message}")

    @staticmethod
    def warning(message):
        print(f"WARNING: {message}")

    @staticmethod
    def error(message):
        raise RuntimeError(message)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--flver", required=True, type=Path)
    parser.add_argument("--base-anibnd", required=True, type=Path)
    parser.add_argument("--animation-anibnd", required=True, type=Path)
    parser.add_argument("--animation", required=True)
    parser.add_argument("--output-blend", required=True, type=Path)
    parser.add_argument("--metadata", required=True, type=Path)
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(arguments)


def normalized_angle_degrees(value: float) -> float:
    return (value + 180.0) % 360.0 - 180.0


def rounded(value: float) -> float:
    return round(float(value), 7)


def load_animation(args: argparse.Namespace):
    from soulstruct.blender.animation.types import SoulstructAnimation
    from soulstruct.blender.animation.utilities import read_animation_hkx_entry, read_skeleton_hkx_entry
    from soulstruct.blender.flver.models.types.bl_flver._import_bones import (
        create_edit_bones,
        write_flver_rest_pose_to_edit_bones,
    )
    from soulstruct.containers import EntryNotFoundError
    from soulstruct.eldenring.containers import DivBinder
    from soulstruct.flver import FLVER
    from soulstruct.havok.core import HKX

    scene = bpy.context.scene
    scene.soulstruct_settings.game_enum = "ELDEN_RING"
    scene.flver_import_settings.import_textures = False
    scene.animation_import_settings.to_60_fps = False
    scene.render.fps = 30

    flver = FLVER.from_path(args.flver)
    bone_names: list[str] = []
    for bone in flver.bones:
        bone_name = f"{bone.name} <DUPE>" if bone.name in bone_names else bone.name
        bone_names.append(bone_name)

    armature_data = bpy.data.armatures.new(f"{args.flver.stem} Armature")
    armature = bpy.data.objects.new(args.flver.stem, armature_data)
    scene.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT", toggle=False)
    edit_bones = create_edit_bones(flver, armature_data, bone_names)
    write_flver_rest_pose_to_edit_bones(PipelineLog(), flver, edit_bones)
    del edit_bones
    bpy.ops.object.mode_set(mode="OBJECT", toggle=False)

    base_binder = DivBinder.from_path(args.base_anibnd)
    animation_binder = DivBinder.from_path(args.animation_anibnd)
    skeleton_entry = base_binder.find_entry_matching_name(r"Skeleton\.hkx(\.dcx)?$")
    try:
        compendium_entry = animation_binder.find_entry_matching_name(r".*\.compendium$")
    except EntryNotFoundError:
        compendium = None
    else:
        compendium = HKX.from_binder_entry(compendium_entry)

    skeleton_hkx = read_skeleton_hkx_entry(skeleton_entry, compendium)
    animation_pattern = rf"(^|.*[\\/]){re.escape(args.animation)}\.hkx(\.dcx)?$"
    animation_entry = animation_binder.find_entry_matching_name(animation_pattern)
    animation_hkx = read_animation_hkx_entry(animation_entry, compendium)
    animation = SoulstructAnimation.new_from_hkx_animation(
        PipelineLog(),
        bpy.context,
        animation_hkx,
        skeleton_hkx,
        name=args.animation,
        armature_obj=armature,
        model_name=args.flver.stem,
    )
    return armature, animation.action, animation_entry.name


def rest_skeleton_height(armature) -> float:
    points = [bone.head_local for bone in armature.data.bones]
    points.extend(bone.tail_local for bone in armature.data.bones)
    return max(point.z for point in points) - min(point.z for point in points)


def collect_motion_metadata(armature, action) -> dict:
    scene = bpy.context.scene
    frame_start, frame_end = action.frame_range
    integer_start = int(round(frame_start))
    integer_end = int(round(frame_end))
    body_names = sorted(name for name in BODY_BONES if name in armature.pose.bones)
    missing_body_bones = sorted(BODY_BONES.difference(body_names))
    digest = hashlib.sha256()
    invalid_transform_count = 0
    minimum_pose_height = math.inf
    maximum_pose_extent = 0.0
    root_positions: list[list[float]] = []
    root_yaws: list[float] = []

    for frame in range(integer_start, integer_end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        positions = {}
        for bone_name in body_names:
            pose_bone = armature.pose.bones[bone_name]
            position = armature.matrix_world @ pose_bone.head
            quaternion = pose_bone.matrix.to_quaternion()
            values = [position.x, position.y, position.z, quaternion.w, quaternion.x, quaternion.y, quaternion.z]
            if not all(math.isfinite(value) for value in values):
                invalid_transform_count += 1
            canonical = [frame, bone_name, *(rounded(value) for value in values)]
            digest.update(json.dumps(canonical, separators=(",", ":")).encode("ascii"))
            digest.update(b"\n")
            positions[bone_name] = position

        if "Pelvis" not in positions or "RootRotY" not in positions:
            raise RuntimeError("Imported animation is missing Pelvis or RootRotY in the sampled body set.")
        pose_height = max(position.z for position in positions.values()) - min(position.z for position in positions.values())
        minimum_pose_height = min(minimum_pose_height, pose_height)
        pelvis = positions["Pelvis"]
        maximum_pose_extent = max(
            maximum_pose_extent,
            max((position - pelvis).length for position in positions.values()),
        )
        root = positions["RootRotY"]
        root_positions.append([root.x, root.y, root.z])
        root_quaternion = armature.pose.bones["RootRotY"].matrix.to_quaternion()
        root_yaws.append(math.degrees(root_quaternion.to_euler("XYZ").z))

    translation = [root_positions[-1][index] - root_positions[0][index] for index in range(3)]
    root_yaw_delta = normalized_angle_degrees(root_yaws[-1] - root_yaws[0])
    return {
        "frameStart": frame_start,
        "frameEnd": frame_end,
        "frameCount": integer_end - integer_start + 1,
        "frameRate": scene.render.fps,
        "durationSeconds": (integer_end - integer_start + 1) / scene.render.fps,
        "armatureBoneCount": len(armature.data.bones),
        "sampledBodyBoneCount": len(body_names),
        "missingBodyBones": missing_body_bones,
        "invalidTransformCount": invalid_transform_count,
        "minimumPoseHeight": minimum_pose_height,
        "maximumPoseExtentFromPelvis": maximum_pose_extent,
        "restSkeletonHeight": rest_skeleton_height(armature),
        "rootTranslation": translation,
        "rootTranslationMagnitude": math.sqrt(sum(value * value for value in translation)),
        "rootYawDeltaDegrees": root_yaw_delta,
        "motionFingerprintSHA256": digest.hexdigest(),
    }


def main() -> None:
    args = parse_args()
    for path in (args.flver, args.base_anibnd, args.animation_anibnd):
        if not path.is_file():
            raise FileNotFoundError(path)
    args.output_blend.parent.mkdir(parents=True, exist_ok=True)
    args.metadata.parent.mkdir(parents=True, exist_ok=True)

    gpu.init()
    bpy.ops.preferences.addon_enable(module="io_soulstruct")
    armature, action, animation_entry = load_animation(args)
    scene = bpy.context.scene
    scene.frame_start = int(round(action.frame_range[0]))
    scene.frame_end = int(round(action.frame_range[1]))
    armature["pipelineAnimationId"] = args.animation
    armature["pipelineSourceEntry"] = animation_entry
    armature["pipelineCoordinateSpace"] = "BlenderZUp_SourceNative"
    scene["pipelineAnimationId"] = args.animation
    scene["pipelinePhase"] = "Phase5HKXBlenderPilot"

    motion = collect_motion_metadata(armature, action)
    save_result = bpy.ops.wm.save_as_mainfile(filepath=str(args.output_blend), check_existing=False)
    if save_result != {"FINISHED"}:
        raise RuntimeError(f"Blender save failed: {save_result}")

    metadata = {
        "schemaVersion": 1,
        "sourceFlver": str(args.flver),
        "sourceBaseAnibnd": str(args.base_anibnd),
        "sourceAnimationAnibnd": str(args.animation_anibnd),
        "sourceAnimationId": args.animation,
        "sourceAnimationEntry": animation_entry,
        "blenderVersion": bpy.app.version_string,
        "blenderPath": str(args.output_blend),
        "coordinateSpace": "BlenderZUp_SourceNative",
        **motion,
    }
    args.metadata.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(
        f"PIPELINE_CONVERSION_RESULT={args.animation};frames={metadata['frameCount']};"
        f"fingerprint={metadata['motionFingerprintSHA256']}"
    )


try:
    main()
except Exception:
    traceback.print_exc()
    raise
