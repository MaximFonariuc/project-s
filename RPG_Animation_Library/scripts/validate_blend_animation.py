#!/usr/bin/env python3
"""Reopen a Phase 5 Blender artifact and verify that its action survived serialization."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
import traceback
from pathlib import Path

import bpy


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


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--expected-metadata", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(arguments)


def rounded(value: float) -> float:
    return round(float(value), 7)


def motion_fingerprint(armature, frame_start: int, frame_end: int) -> str:
    digest = hashlib.sha256()
    body_names = sorted(name for name in BODY_BONES if name in armature.pose.bones)
    for frame in range(frame_start, frame_end + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        for bone_name in body_names:
            pose_bone = armature.pose.bones[bone_name]
            position = armature.matrix_world @ pose_bone.head
            quaternion = pose_bone.matrix.to_quaternion()
            values = [position.x, position.y, position.z, quaternion.w, quaternion.x, quaternion.y, quaternion.z]
            if not all(math.isfinite(value) for value in values):
                raise RuntimeError(f"Non-finite transform after reload: frame={frame} bone={bone_name}")
            canonical = [frame, bone_name, *(rounded(value) for value in values)]
            digest.update(json.dumps(canonical, separators=(",", ":")).encode("ascii"))
            digest.update(b"\n")
    return digest.hexdigest()


def main() -> None:
    args = parse_args()
    expected = json.loads(args.expected_metadata.read_text(encoding="utf-8"))
    animation_id = expected["sourceAnimationId"]
    armatures = [
        candidate
        for candidate in bpy.data.objects
        if candidate.type == "ARMATURE" and candidate.get("pipelineAnimationId") == animation_id
    ]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one pipeline armature for {animation_id}, found {len(armatures)}")
    armature = armatures[0]
    action = armature.animation_data.action if armature.animation_data is not None else None
    if action is None:
        raise RuntimeError(f"Serialized armature {armature.name} has no active action")

    frame_start = int(round(action.frame_range[0]))
    frame_end = int(round(action.frame_range[1]))
    frame_count = frame_end - frame_start + 1
    fingerprint = motion_fingerprint(armature, frame_start, frame_end)
    checks = {
        "animationId": armature.get("pipelineAnimationId") == animation_id,
        "sourceEntry": armature.get("pipelineSourceEntry") == expected["sourceAnimationEntry"],
        "coordinateSpace": armature.get("pipelineCoordinateSpace") == expected["coordinateSpace"],
        "frameStart": frame_start == int(round(expected["frameStart"])),
        "frameEnd": frame_end == int(round(expected["frameEnd"])),
        "frameCount": frame_count == expected["frameCount"],
        "frameRate": bpy.context.scene.render.fps == expected["frameRate"],
        "armatureBoneCount": len(armature.data.bones) == expected["armatureBoneCount"],
        "motionFingerprint": fingerprint == expected["motionFingerprintSHA256"],
    }
    failures = [name for name, passed in checks.items() if not passed]
    result = {
        "schemaVersion": 1,
        "status": "PASS" if not failures else "FAIL",
        "animationId": animation_id,
        "blendFile": bpy.data.filepath,
        "actionName": action.name,
        "frameStart": frame_start,
        "frameEnd": frame_end,
        "frameCount": frame_count,
        "frameRate": bpy.context.scene.render.fps,
        "armatureBoneCount": len(armature.data.bones),
        "motionFingerprintSHA256": fingerprint,
        "checks": checks,
        "failures": failures,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"PIPELINE_REOPEN_RESULT={animation_id};status={result['status']};fingerprint={fingerprint}")
    if failures:
        raise RuntimeError("Serialized Blender validation failed: " + ", ".join(failures))


try:
    main()
except Exception:
    traceback.print_exc()
    raise
