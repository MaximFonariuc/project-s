#!/usr/bin/env python3
"""Shared Blender pose-motion measurements for retarget and FBX validation gates."""

from __future__ import annotations

import math

import bpy


ROTATION_THRESHOLD_DEGREES = 0.1
TRANSLATION_THRESHOLD_METERS = 0.0001


def measure_pose_motion(armature, bone_names: list[str], frame_start: int, frame_end: int) -> dict:
    scene = bpy.context.scene
    original_frame = scene.frame_current
    initial = {}
    maxima = {
        name: {"maxRotationDegrees": 0.0, "maxTranslationMeters": 0.0}
        for name in bone_names
    }

    for frame in range(frame_start, frame_end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        for name in bone_names:
            pose_bone = armature.pose.bones[name]
            translation, rotation, _ = pose_bone.matrix_basis.decompose()
            rotation.normalize()
            if name not in initial:
                initial[name] = (translation.copy(), rotation.copy())
                continue

            initial_translation, initial_rotation = initial[name]
            rotation_degrees = math.degrees(initial_rotation.rotation_difference(rotation).angle)
            translation_meters = (translation - initial_translation).length
            maxima[name]["maxRotationDegrees"] = max(
                maxima[name]["maxRotationDegrees"], float(rotation_degrees)
            )
            maxima[name]["maxTranslationMeters"] = max(
                maxima[name]["maxTranslationMeters"], float(translation_meters)
            )

    scene.frame_set(original_frame)
    bpy.context.view_layer.update()
    per_bone = {}
    for name in bone_names:
        rotation = maxima[name]["maxRotationDegrees"]
        translation = maxima[name]["maxTranslationMeters"]
        per_bone[name] = {
            "maxRotationDegrees": round(rotation, 6),
            "maxTranslationMeters": round(translation, 9),
            "animated": rotation >= ROTATION_THRESHOLD_DEGREES or translation >= TRANSLATION_THRESHOLD_METERS,
        }

    animated = [name for name, values in per_bone.items() if values["animated"]]
    return {
        "rotationThresholdDegrees": ROTATION_THRESHOLD_DEGREES,
        "translationThresholdMeters": TRANSLATION_THRESHOLD_METERS,
        "animatedBoneCount": len(animated),
        "animatedBones": animated,
        "totalMaxRotationDegrees": round(
            sum(values["maxRotationDegrees"] for values in per_bone.values()), 6
        ),
        "maximumBoneRotationDegrees": round(
            max((values["maxRotationDegrees"] for values in per_bone.values()), default=0.0), 6
        ),
        "maximumBoneTranslationMeters": round(
            max((values["maxTranslationMeters"] for values in per_bone.values()), default=0.0), 9
        ),
        "perBone": per_bone,
    }
