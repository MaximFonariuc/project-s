#!/usr/bin/env python3
"""Reopen and validate a baked Phase 7 target action."""

from __future__ import annotations

import argparse
import hashlib
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
    parser.add_argument("--expected-metadata", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args(arguments)


def rounded_vector(vector) -> list[float]:
    return [round(float(component), 7) for component in vector]


def rounded_quaternion(quaternion) -> list[float]:
    value = quaternion.normalized()
    if value.w < 0.0:
        value = type(value)((-value.w, -value.x, -value.y, -value.z))
    return [round(float(component), 7) for component in value]


def fingerprint(armature, bones: list[str], frames: list[int]) -> str:
    values = []
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        values.append(
            {
                "frame": frame,
                "objectTranslation": rounded_vector(armature.matrix_world.translation),
                "objectRotation": rounded_quaternion(armature.matrix_world.to_quaternion()),
                "bones": {
                    name: {
                        "translation": rounded_vector(armature.pose.bones[name].matrix.translation),
                        "rotation": rounded_quaternion(armature.pose.bones[name].matrix.to_quaternion()),
                    }
                    for name in bones
                },
            }
        )
    encoded = json.dumps(values, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def main() -> int:
    args = parse_args()
    expected = json.loads(args.expected_metadata.read_text(encoding="utf-8"))
    armature = bpy.data.objects.get(expected["targetArmatureName"])
    failures = []
    missing = []
    actual_pose_motion = None
    if armature is None or armature.type != "ARMATURE":
        failures.append("target armature missing")
    elif armature.animation_data is None or armature.animation_data.action is None:
        failures.append("target action missing")
    else:
        missing = sorted(name for name in expected["mappedTargetBones"] if name not in armature.pose.bones)
        if missing:
            failures.append("mapped bones missing: " + "|".join(missing))
        else:
            actual_fingerprint = fingerprint(
                armature,
                expected["mappedTargetBones"],
                expected["fingerprintFrames"],
            )
            if actual_fingerprint != expected["motionFingerprintSHA256"]:
                failures.append("motion fingerprint mismatch")
            frame_start = int(expected["frameStart"])
            frame_end = int(expected["frameEnd"])
            actual_pose_motion = measure_pose_motion(
                armature,
                expected["mappedTargetBones"],
                frame_start,
                frame_end,
            )
            expected_pose_motion = expected.get("poseMotion")
            if expected_pose_motion is None:
                failures.append("pose motion evidence missing")
            elif actual_pose_motion["animatedBoneCount"] != expected_pose_motion["animatedBoneCount"]:
                failures.append(
                    "animated bone count mismatch: "
                    f"expected={expected_pose_motion['animatedBoneCount']} "
                    f"actual={actual_pose_motion['animatedBoneCount']}"
                )
            bpy.context.scene.frame_set(frame_start)
            bpy.context.view_layer.update()
            root_start = armature.matrix_world.translation.copy()
            bpy.context.scene.frame_set(frame_end)
            bpy.context.view_layer.update()
            root_delta = armature.matrix_world.translation - root_start
            expected_root = expected["targetRootTranslation"]
            root_error = math.sqrt(sum((root_delta[index] - expected_root[index]) ** 2 for index in range(3)))
            if root_error > 0.00001:
                failures.append(f"root translation mismatch: {root_error:.9f}")

    result = {
        "schemaVersion": 1,
        "status": "PASS" if not failures else "FAIL",
        "animationId": expected["animationId"],
        "motionFingerprintSHA256": expected.get("motionFingerprintSHA256", ""),
        "poseMotion": actual_pose_motion,
        "failures": failures,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"RETARGET_REOPEN_{result['status']}:{expected['animationId']}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
