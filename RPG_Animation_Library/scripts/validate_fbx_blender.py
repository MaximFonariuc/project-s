#!/usr/bin/env python3
"""Reimport a Phase 8 FBX and validate its armature, timing and root trajectory."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from animation_motion_blender import measure_pose_motion


def parse_args() -> argparse.Namespace:
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fbx", required=True, type=Path)
    parser.add_argument("--retarget-metadata", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args(arguments)


def rounded_vector(vector) -> list[float]:
    return [round(float(component), 7) for component in vector]


def main() -> int:
    args = parse_args()
    expected = json.loads(args.retarget_metadata.read_text(encoding="utf-8"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(args.fbx.resolve()), use_anim=True)
    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    failures = []
    if len(armatures) != 1:
        failures.append(f"expected one armature, found {len(armatures)}")
        armature = None
    else:
        armature = armatures[0]

    actual_frame_start = None
    actual_frame_end = None
    actual_frame_count = 0
    root_translation = [0.0, 0.0, 0.0]
    pose_motion = None
    missing_bones = []
    if armature is not None:
        missing_bones = sorted(name for name in expected["mappedTargetBones"] if name not in armature.pose.bones)
        if missing_bones:
            failures.append("mapped bones missing: " + "|".join(missing_bones))
        if armature.animation_data is None or armature.animation_data.action is None:
            failures.append("animation action missing")
        else:
            actual_export_start, actual_export_end = (
                int(round(value)) for value in armature.animation_data.action.frame_range
            )
            expected_frame_count = int(expected["frameCount"])
            imported_bind_pose_frame = 1
            imported_clip_frame_start = 2
            imported_clip_frame_end = expected_frame_count + 1
            actual_frame_start = 1
            actual_frame_end = expected_frame_count
            actual_frame_count = actual_export_end - imported_clip_frame_start + 1
            if actual_export_start != imported_bind_pose_frame:
                failures.append(
                    "bind pose prefix missing: "
                    f"expected={imported_bind_pose_frame} actual={actual_export_start}"
                )
            if actual_export_end != imported_clip_frame_end:
                failures.append(
                    "export frame end mismatch: "
                    f"expected={imported_clip_frame_end} actual={actual_export_end}"
                )
            if actual_frame_count != expected_frame_count:
                failures.append(
                    f"frame count mismatch: expected={expected['frameCount']} actual={actual_frame_count}"
                )
            pose_motion = measure_pose_motion(
                armature,
                expected["mappedTargetBones"],
                imported_clip_frame_start,
                imported_clip_frame_end,
            )
            expected_pose_motion = expected.get("poseMotion")
            if expected_pose_motion is None:
                failures.append("retarget pose motion evidence missing")
            else:
                expected_animated = int(expected_pose_motion["animatedBoneCount"])
                minimum_animated = max(1, math.ceil(expected_animated * 0.5))
                if pose_motion["animatedBoneCount"] < minimum_animated:
                    failures.append(
                        "pose motion collapsed: "
                        f"expectedAnimated={expected_animated} "
                        f"actualAnimated={pose_motion['animatedBoneCount']}"
                    )
                if pose_motion["totalMaxRotationDegrees"] < (
                    float(expected_pose_motion["totalMaxRotationDegrees"]) * 0.2
                ):
                    failures.append("pose rotation extent collapsed")
            bpy.context.scene.frame_set(imported_clip_frame_start)
            bpy.context.view_layer.update()
            root_start = armature.matrix_world.translation.copy()
            bpy.context.scene.frame_set(imported_clip_frame_end)
            bpy.context.view_layer.update()
            root_delta = armature.matrix_world.translation - root_start
            root_translation = rounded_vector(root_delta)
            expected_root = expected["targetRootTranslation"]
            root_error = math.sqrt(sum((root_delta[index] - expected_root[index]) ** 2 for index in range(3)))
            if root_error > 0.001:
                failures.append(f"root translation mismatch: {root_error:.9f}")

    result = {
        "schemaVersion": 1,
        "status": "PASS" if not failures else "FAIL",
        "animationId": expected["animationId"],
        "armatureCount": len(armatures),
        "armature": armature.name if armature else None,
        "boneCount": len(armature.data.bones) if armature else 0,
        "missingMappedBones": missing_bones,
        "frameStart": actual_frame_start,
        "frameEnd": actual_frame_end,
        "frameCount": actual_frame_count,
        "bindPoseFrame": 0 if actual_frame_start is not None else None,
        "rootTranslation": root_translation,
        "poseMotion": pose_motion,
        "failures": failures,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"FBX_REIMPORT_{result['status']}:{expected['animationId']}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
