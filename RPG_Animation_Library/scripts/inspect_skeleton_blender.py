#!/usr/bin/env python3
"""Inspect one Blender or FBX armature and emit a deterministic skeleton report."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy


def parse_args() -> argparse.Namespace:
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--blend", type=Path)
    source.add_argument("--fbx", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--sample-bone", action="append", default=[])
    parser.add_argument("--import-animation", action="store_true")
    return parser.parse_args(arguments)


def rounded_vector(vector) -> list[float]:
    return [round(float(component), 9) for component in vector]


def rounded_quaternion(quaternion) -> list[float]:
    return [round(float(component), 9) for component in quaternion]


def finite(values: list[float]) -> bool:
    return all(math.isfinite(value) for value in values)


def load_scene(args: argparse.Namespace) -> Path:
    if args.blend:
        path = args.blend.resolve()
        bpy.ops.wm.open_mainfile(filepath=str(path))
        return path

    path = args.fbx.resolve()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=args.import_animation)
    return path


def main() -> int:
    args = parse_args()
    source_path = load_scene(args)
    armatures = sorted(
        (obj for obj in bpy.data.objects if obj.type == "ARMATURE"),
        key=lambda obj: obj.name,
    )
    if len(armatures) != 1:
        raise RuntimeError(f"Expected exactly one armature, found {len(armatures)}")

    armature = armatures[0]
    bones = []
    world_points = []
    for bone in armature.data.bones:
        head_world = armature.matrix_world @ bone.head_local
        tail_world = armature.matrix_world @ bone.tail_local
        world_points.extend((head_world, tail_world))
        local_matrix = bone.matrix_local
        translation, rotation, scale = local_matrix.decompose()
        values = [*translation, *rotation, *scale, *head_world, *tail_world]
        if not finite([float(value) for value in values]):
            raise RuntimeError(f"Non-finite rest transform on bone {bone.name}")
        bones.append(
            {
                "name": bone.name,
                "parent": bone.parent.name if bone.parent else None,
                "useDeform": bool(bone.use_deform),
                "headWorld": rounded_vector(head_world),
                "tailWorld": rounded_vector(tail_world),
                "lengthWorld": round(float((tail_world - head_world).length), 9),
                "restLocalTranslation": rounded_vector(translation),
                "restLocalRotationWXYZ": rounded_quaternion(rotation),
                "restLocalScale": rounded_vector(scale),
            }
        )

    minimum = [min(float(point[index]) for point in world_points) for index in range(3)]
    maximum = [max(float(point[index]) for point in world_points) for index in range(3)]
    dimensions = [maximum[index] - minimum[index] for index in range(3)]
    object_translation, object_rotation, object_scale = armature.matrix_world.decompose()
    action = armature.animation_data.action if armature.animation_data else None
    samples = []
    if action is not None and args.sample_bone:
        frame_start, frame_end = (int(round(value)) for value in action.frame_range)
        for frame in sorted({frame_start, (frame_start + frame_end) // 2, frame_end}):
            bpy.context.scene.frame_set(frame)
            values = {}
            for bone_name in args.sample_bone:
                pose_bone = armature.pose.bones.get(bone_name)
                if pose_bone is None:
                    values[bone_name] = None
                    continue
                basis_translation, basis_rotation, basis_scale = pose_bone.matrix_basis.decompose()
                world_matrix = armature.matrix_world @ pose_bone.matrix
                world_translation, world_rotation, _ = world_matrix.decompose()
                values[bone_name] = {
                    "basisTranslation": rounded_vector(basis_translation),
                    "basisRotationWXYZ": rounded_quaternion(basis_rotation),
                    "basisScale": rounded_vector(basis_scale),
                    "worldTranslation": rounded_vector(world_translation),
                    "worldRotationWXYZ": rounded_quaternion(world_rotation),
                }
            samples.append({"frame": frame, "bones": values})
    report = {
        "schemaVersion": 1,
        "sourcePath": source_path.as_posix(),
        "blenderVersion": bpy.app.version_string,
        "armature": armature.name,
        "boneCount": len(bones),
        "rootBones": [bone["name"] for bone in bones if bone["parent"] is None],
        "boundsWorld": {
            "minimum": rounded_vector(minimum),
            "maximum": rounded_vector(maximum),
            "dimensions": rounded_vector(dimensions),
        },
        "objectTransform": {
            "translation": rounded_vector(object_translation),
            "rotationWXYZ": rounded_quaternion(object_rotation),
            "scale": rounded_vector(object_scale),
        },
        "action": None
        if action is None
        else {
            "name": action.name,
            "frameRange": [round(float(value), 6) for value in action.frame_range],
            "slotCount": len(action.slots),
        },
        "samples": samples,
        "bones": bones,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"SKELETON_INSPECTION_OK:{args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
