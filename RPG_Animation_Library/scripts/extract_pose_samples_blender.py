#!/usr/bin/env python3
"""Import one Elden Ring HKX animation in Blender and export sampled skeleton poses."""

from __future__ import annotations

import argparse
import json
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
    """Minimal Soulstruct-compatible logger for a non-interactive Blender run."""

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
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--samples", type=int, default=6)
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(arguments)


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


def sample_animation(args: argparse.Namespace, armature, action, animation_entry: str) -> dict:
    frame_start, frame_end = action.frame_range
    if args.samples < 2:
        sample_frames = [round(frame_start)]
    else:
        sample_frames = [
            round(frame_start + (frame_end - frame_start) * index / (args.samples - 1))
            for index in range(args.samples)
        ]

    body_parents: dict[str, str] = {}
    sample_positions: list[dict[str, list[float]]] = []
    root_positions: list[list[float]] = []

    for frame in sample_frames:
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        positions = {
            pose_bone.name: armature.matrix_world @ pose_bone.head
            for pose_bone in armature.pose.bones
            if pose_bone.name in BODY_BONES
        }
        if "Pelvis" not in positions:
            raise RuntimeError("Pelvis bone was not found in the imported c0000 skeleton.")

        center = positions["Pelvis"]
        root = positions.get("RootRotY", center)
        root_positions.append([root.x, root.y, root.z])
        sample_positions.append(
            {
                name: [position.x - center.x, position.y - center.y, position.z]
                for name, position in positions.items()
            }
        )

        for pose_bone in armature.pose.bones:
            if pose_bone.name not in positions:
                continue
            parent = pose_bone.parent
            while parent is not None and parent.name not in positions:
                parent = parent.parent
            if parent is not None:
                body_parents[pose_bone.name] = parent.name

    return {
        "sourceAnimationId": args.animation,
        "sourceAnimationEntry": animation_entry,
        "frameStart": frame_start,
        "frameEnd": frame_end,
        "frameCount": int(frame_end - frame_start + 1),
        "frameRate": 30,
        "sampleFrames": sample_frames,
        "rootPositions": root_positions,
        "samplePositions": sample_positions,
        "bodyParents": body_parents,
        "blenderVersion": bpy.app.version_string,
    }


def main() -> None:
    args = parse_args()
    for path in (args.flver, args.base_anibnd, args.animation_anibnd):
        if not path.is_file():
            raise FileNotFoundError(path)
    if args.samples < 1:
        raise ValueError("--samples must be positive")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    gpu.init()
    bpy.ops.preferences.addon_enable(module="io_soulstruct")
    armature, action, animation_entry = load_animation(args)
    metadata = sample_animation(args, armature, action, animation_entry)
    args.output.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(
        f"PIPELINE_POSE_RESULT={metadata['sourceAnimationId']};"
        f"frames={metadata['frameCount']};samples={len(metadata['sampleFrames'])}"
    )


try:
    main()
except Exception:
    traceback.print_exc()
    raise
