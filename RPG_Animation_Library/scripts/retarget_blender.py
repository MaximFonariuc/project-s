#!/usr/bin/env python3
"""Bake one converted ER_Player action onto the Project S master Humanoid rig."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from animation_motion_blender import measure_pose_motion


def parse_args() -> argparse.Namespace:
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-blend", required=True, type=Path)
    parser.add_argument("--target-fbx", required=True, type=Path)
    parser.add_argument("--source-profile", required=True, type=Path)
    parser.add_argument("--target-profile", required=True, type=Path)
    parser.add_argument("--retarget-map", required=True, type=Path)
    parser.add_argument("--animation-id", required=True)
    parser.add_argument("--library-id", required=True)
    parser.add_argument("--output-blend", required=True, type=Path)
    parser.add_argument("--output-metadata", required=True, type=Path)
    return parser.parse_args(arguments)


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def rounded_vector(vector, digits: int = 7) -> list[float]:
    return [round(float(component), digits) for component in vector]


def rounded_quaternion(quaternion, digits: int = 7) -> list[float]:
    value = quaternion.normalized()
    if value.w < 0.0:
        value = Quaternion((-value.w, -value.x, -value.y, -value.z))
    return [round(float(component), digits) for component in value]


def sample_frames(frame_start: int, frame_end: int, count: int = 7) -> list[int]:
    if frame_start == frame_end or count <= 1:
        return [frame_start]
    return sorted(
        {
            round(frame_start + (frame_end - frame_start) * index / (count - 1))
            for index in range(count)
        }
    )


def matrix_fingerprint(
    armature,
    mapped_target_bones: list[str],
    frames: list[int],
) -> str:
    values = []
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        object_matrix = armature.matrix_world
        values.append(
            {
                "frame": frame,
                "objectTranslation": rounded_vector(object_matrix.translation),
                "objectRotation": rounded_quaternion(object_matrix.to_quaternion()),
                "bones": {
                    bone_name: {
                        "translation": rounded_vector(armature.pose.bones[bone_name].matrix.translation),
                        "rotation": rounded_quaternion(armature.pose.bones[bone_name].matrix.to_quaternion()),
                    }
                    for bone_name in mapped_target_bones
                },
            }
        )
    encoded = json.dumps(values, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def reset_pose(armature) -> None:
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.rotation_quaternion = Quaternion((1.0, 0.0, 0.0, 0.0))
        pose_bone.scale = (1.0, 1.0, 1.0)


def bone_depth(pose_bone) -> int:
    depth = 0
    parent = pose_bone.parent
    while parent is not None:
        depth += 1
        parent = parent.parent
    return depth


def set_absolute_rotation(pose_bone, rotation: Quaternion) -> None:
    current = pose_bone.matrix.copy()
    pose_bone.matrix = Matrix.LocRotScale(current.translation, rotation.normalized(), Vector((1.0, 1.0, 1.0)))


def bake_action(
    source,
    target,
    mappings: list[dict[str, str]],
    frame_start: int,
    frame_end: int,
    world_height_ratio: float,
    preserve_vertical_root: bool,
) -> tuple[Vector, Vector]:
    scene = bpy.context.scene
    scene.frame_set(frame_start)
    bpy.context.view_layer.update()
    source_initial_matrix = source.matrix_world.copy()
    target_initial_location = target.location.copy()
    target_initial_rotation = target.rotation_quaternion.copy()
    target.rotation_mode = "QUATERNION"

    target_scale = sum(abs(float(component)) for component in target.scale) / 3.0
    if target_scale <= 1e-8:
        raise RuntimeError("Target armature object has an invalid scale")
    armature_space_scale = world_height_ratio / target_scale

    source_hips = source.pose.bones["Pelvis"]
    target_hips = target.pose.bones["pelvis"]
    source_hips_rest_translation = source_hips.bone.matrix_local.translation.copy()
    target_hips_rest_translation = target_hips.bone.matrix_local.translation.copy()

    sorted_mappings = sorted(mappings, key=lambda item: bone_depth(target.pose.bones[item["target"]]))
    source_rest_rotations = {
        item["source"]: source.pose.bones[item["source"]].bone.matrix_local.to_quaternion()
        for item in mappings
    }
    target_rest_rotations = {
        item["target"]: target.pose.bones[item["target"]].bone.matrix_local.to_quaternion()
        for item in mappings
    }

    action = bpy.data.actions.new(name=f"{source.animation_data.action.name}__ProjectS")
    target.animation_data_create()
    target.animation_data.action = action
    action.use_fake_user = True

    for frame in range(frame_start, frame_end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        source_matrix = source.matrix_world.copy()
        root_delta = source_matrix.translation - source_initial_matrix.translation
        if not preserve_vertical_root:
            root_delta.z = 0.0
        target.location = target_initial_location + root_delta * world_height_ratio
        source_rotation_delta = source_matrix.to_quaternion() @ source_initial_matrix.to_quaternion().inverted()
        target.rotation_quaternion = (source_rotation_delta @ target_initial_rotation).normalized()
        target.keyframe_insert(data_path="location", frame=frame, group="RootMotion")
        target.keyframe_insert(data_path="rotation_quaternion", frame=frame, group="RootMotion")

        reset_pose(target)
        bpy.context.view_layer.update()
        desired_rotations: dict[str, Quaternion] = {}
        for item in mappings:
            source_name = item["source"]
            target_name = item["target"]
            source_pose_rotation = source.pose.bones[source_name].matrix.to_quaternion()
            source_delta = source_pose_rotation @ source_rest_rotations[source_name].inverted()
            desired_rotations[target_name] = (source_delta @ target_rest_rotations[target_name]).normalized()

        for item in sorted_mappings:
            target_name = item["target"]
            target_pose_bone = target.pose.bones[target_name]
            set_absolute_rotation(target_pose_bone, desired_rotations[target_name])
            if target_name == "pelvis":
                source_delta_translation = source_hips.matrix.translation - source_hips_rest_translation
                target_pose_bone.matrix.translation = (
                    target_hips_rest_translation + source_delta_translation * armature_space_scale
                )
            bpy.context.view_layer.update()
            target_pose_bone.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=target_name)
            if target_name == "pelvis":
                target_pose_bone.keyframe_insert(data_path="location", frame=frame, group=target_name)

    scene.frame_start = frame_start
    scene.frame_end = frame_end
    scene.frame_set(frame_start)
    bpy.context.view_layer.update()
    source.hide_set(True)
    source.hide_render = True

    scene.frame_set(frame_end)
    bpy.context.view_layer.update()
    target_root_translation = target.matrix_world.translation.copy()
    scene.frame_set(frame_start)
    bpy.context.view_layer.update()
    target_root_translation -= target.matrix_world.translation
    source_root_translation = source_matrix.translation - source_initial_matrix.translation
    return source_root_translation, target_root_translation


def collect_pose_evidence(
    target,
    body_bones: list[str],
    frames: list[int],
) -> tuple[list[dict], dict[str, str], float, float]:
    samples = []
    parents: dict[str, str] = {}
    minimum_height = math.inf
    maximum_extent = 0.0
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        positions = {
            name: target.matrix_world @ target.pose.bones[name].head
            for name in body_bones
        }
        pelvis = positions["pelvis"]
        heights = [position.z for position in positions.values()]
        minimum_height = min(minimum_height, max(heights) - min(heights))
        maximum_extent = max(maximum_extent, max((position - pelvis).length for position in positions.values()))
        samples.append(
            {
                "frame": frame,
                "positions": {
                    name: rounded_vector(Vector((position.x - pelvis.x, position.y - pelvis.y, position.z)))
                    for name, position in positions.items()
                },
            }
        )
        for name in body_bones:
            parent = target.pose.bones[name].parent
            while parent is not None and parent.name not in positions:
                parent = parent.parent
            if parent is not None:
                parents[name] = parent.name
    return samples, parents, minimum_height, maximum_extent


def main() -> int:
    args = parse_args()
    for path in (
        args.source_blend,
        args.target_fbx,
        args.source_profile,
        args.target_profile,
        args.retarget_map,
    ):
        if not path.is_file():
            raise FileNotFoundError(path)

    source_profile = load_json(args.source_profile)
    target_profile = load_json(args.target_profile)
    retarget_map = load_json(args.retarget_map)
    if retarget_map["sourceProfile"] != source_profile["id"]:
        raise RuntimeError("Retarget map source profile mismatch")
    if retarget_map["targetProfile"] != target_profile["id"]:
        raise RuntimeError("Retarget map target profile mismatch")

    bpy.ops.wm.open_mainfile(filepath=str(args.source_blend.resolve()))
    source = bpy.data.objects.get(source_profile["armatureName"])
    if source is None or source.type != "ARMATURE" or source.animation_data is None or source.animation_data.action is None:
        raise RuntimeError("Converted source armature/action was not found")
    action = source.animation_data.action
    frame_start, frame_end = (int(round(value)) for value in action.frame_range)
    bpy.context.scene.frame_set(frame_start)
    bpy.context.view_layer.update()

    existing = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(args.target_fbx.resolve()), use_anim=False)
    imported_armatures = [obj for obj in bpy.data.objects if obj not in existing and obj.type == "ARMATURE"]
    if len(imported_armatures) != 1:
        raise RuntimeError(f"Expected one imported target armature, found {len(imported_armatures)}")
    target = imported_armatures[0]
    target.name = target_profile["armatureName"]
    target.data.name = "ProjectS_CLazy_Player_Armature"
    target.rotation_mode = "QUATERNION"

    mappings = retarget_map["boneMappings"]
    source_missing = sorted(item["source"] for item in mappings if item["source"] not in source.pose.bones)
    target_missing = sorted(item["target"] for item in mappings if item["target"] not in target.pose.bones)
    if source_missing or target_missing:
        raise RuntimeError(f"Missing mapped bones: source={source_missing}; target={target_missing}")

    world_height_ratio = float(target_profile["restSkeletonHeightMeters"]) / float(
        source_profile["restSkeletonHeightMeters"]
    )
    source_root, target_root = bake_action(
        source,
        target,
        mappings,
        frame_start,
        frame_end,
        world_height_ratio,
        bool(retarget_map["rootMotion"]["preserveVerticalTranslation"]),
    )
    frames = sample_frames(frame_start, frame_end)
    mapped_target_bones = [item["target"] for item in mappings]
    body_bones = list(dict.fromkeys(target_profile["bones"].values()))
    samples, body_parents, minimum_pose_height, maximum_pose_extent = collect_pose_evidence(
        target, body_bones, frames
    )
    fingerprint = matrix_fingerprint(target, mapped_target_bones, frames)
    pose_motion = measure_pose_motion(target, mapped_target_bones, frame_start, frame_end)

    expected_target_root = Vector((source_root.x, source_root.y, 0.0)) * world_height_ratio
    root_error = (target_root - expected_target_root).length
    invalid_count = 0
    for sample in samples:
        for values in sample["positions"].values():
            invalid_count += sum(0 if math.isfinite(value) else 1 for value in values)

    metadata = {
        "schemaVersion": 1,
        "libraryId": args.library_id,
        "animationId": args.animation_id,
        "sourceBlend": args.source_blend.resolve().as_posix(),
        "targetFbx": args.target_fbx.resolve().as_posix(),
        "sourceProfile": source_profile["id"],
        "targetProfile": target_profile["id"],
        "retargetProfile": retarget_map["id"],
        "algorithm": retarget_map["algorithm"],
        "blenderVersion": bpy.app.version_string,
        "targetArmatureName": target.name,
        "targetActionName": target.animation_data.action.name,
        "frameStart": frame_start,
        "frameEnd": frame_end,
        "frameCount": frame_end - frame_start + 1,
        "frameRate": bpy.context.scene.render.fps,
        "durationSeconds": (frame_end - frame_start + 1) / bpy.context.scene.render.fps,
        "mappedBoneCount": len(mappings),
        "mappedTargetBones": mapped_target_bones,
        "bodyBones": body_bones,
        "bodyParents": body_parents,
        "heightScale": world_height_ratio,
        "rootMotionStatus": "PreservedScaledPlanar",
        "sourceRootTranslation": rounded_vector(source_root),
        "targetRootTranslation": rounded_vector(target_root),
        "expectedTargetRootTranslation": rounded_vector(expected_target_root),
        "rootTranslationError": root_error,
        "minimumPoseHeight": minimum_pose_height,
        "maximumPoseExtentFromPelvis": maximum_pose_extent,
        "invalidTransformCount": invalid_count,
        "fingerprintFrames": frames,
        "motionFingerprintSHA256": fingerprint,
        "poseMotion": pose_motion,
        "poseSamples": samples,
    }
    args.output_blend.parent.mkdir(parents=True, exist_ok=True)
    args.output_metadata.parent.mkdir(parents=True, exist_ok=True)
    args.output_metadata.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    bpy.context.scene.frame_set(frame_start)
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output_blend.resolve()), check_existing=False)
    print(
        f"RETARGET_OK:{args.animation_id};frames={metadata['frameCount']};"
        f"bones={metadata['mappedBoneCount']};rootError={root_error:.9f}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
