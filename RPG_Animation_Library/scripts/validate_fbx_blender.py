#!/usr/bin/env python3
"""Reimport a Phase 8 FBX and validate its armature, timing and root trajectory."""

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
    missing_bones = []
    if armature is not None:
        missing_bones = sorted(name for name in expected["mappedTargetBones"] if name not in armature.pose.bones)
        if missing_bones:
            failures.append("mapped bones missing: " + "|".join(missing_bones))
        if armature.animation_data is None or armature.animation_data.action is None:
            failures.append("animation action missing")
        else:
            actual_frame_start, actual_frame_end = (int(round(value)) for value in armature.animation_data.action.frame_range)
            actual_frame_count = actual_frame_end - actual_frame_start + 1
            if actual_frame_count != int(expected["frameCount"]):
                failures.append(
                    f"frame count mismatch: expected={expected['frameCount']} actual={actual_frame_count}"
                )
            bpy.context.scene.frame_set(actual_frame_start)
            bpy.context.view_layer.update()
            root_start = armature.matrix_world.translation.copy()
            bpy.context.scene.frame_set(actual_frame_end)
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
        "rootTranslation": root_translation,
        "failures": failures,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"FBX_REIMPORT_{result['status']}:{expected['animationId']}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
