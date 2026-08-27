"""Batch-convert UE Viewer PSA files to Unity-ready FBX animation files.

The script is executed by Blender in background mode. It deliberately uses the
validated Frank dual-sword armature as the common rest-pose contract so every
body clip has an identical hierarchy before Unity performs Humanoid retargeting.
"""

from __future__ import annotations

import argparse
import json
import math
import pathlib
import sys
import traceback

import bpy
from mathutils import Quaternion


# A single bad source key is distinguishable from fast authored motion when the
# pose jumps far away for exactly one frame and both surrounding frames remain
# close to each other. These conservative limits intentionally leave sustained
# or ambiguous motion untouched.
_OUTLIER_JUMP_DEGREES = 35.0
_OUTLIER_NEIGHBOR_BRIDGE_DEGREES = 20.0
_GLOBAL_CORRUPT_FRAME_MIN_CORE_BONES = 3
_GLOBAL_CORRUPT_FRAME_MIN_RESIDUAL_DEGREES = 5.0
_HUMANOID_BODY_BONES = frozenset(
    {
        "root",
        "pelvis",
        "spine_01",
        "spine_02",
        "spine_03",
        "neck_01",
        "head",
        "clavicle_l",
        "upperarm_l",
        "lowerarm_l",
        "hand_l",
        "clavicle_r",
        "upperarm_r",
        "lowerarm_r",
        "hand_r",
        "thigh_l",
        "calf_l",
        "foot_l",
        "ball_l",
        "thigh_r",
        "calf_r",
        "foot_r",
        "ball_r",
    }
)
_HUMANOID_FINGER_PREFIXES = (
    "thumb_",
    "index_",
    "middle_",
    "ring_",
    "pinky_",
)


def _is_humanoid_body_bone(bone_name: str) -> bool:
    normalized_name = bone_name.rstrip().lower()
    return normalized_name in _HUMANOID_BODY_BONES or normalized_name.startswith(
        _HUMANOID_FINGER_PREFIXES
    )


def _is_humanoid_finger_bone(bone_name: str) -> bool:
    return bone_name.rstrip().lower().startswith(_HUMANOID_FINGER_PREFIXES)


def _normalized_rotation(key) -> Quaternion:
    components = (
        float(key.rotation.w),
        float(key.rotation.x),
        float(key.rotation.y),
        float(key.rotation.z),
    )
    if not all(math.isfinite(component) for component in components):
        raise RuntimeError("PSA contains a non-finite rotation quaternion.")
    rotation = Quaternion(components)
    if rotation.magnitude <= 1e-8:
        raise RuntimeError("PSA contains a zero-length rotation quaternion.")
    rotation.normalize()
    return rotation


def _rotation_angle_degrees(left: Quaternion, right: Quaternion) -> float:
    angle = math.degrees(left.rotation_difference(right).angle)
    # q and -q encode the same orientation. mathutils can report the long
    # representation arc (close to 360 degrees), so always use the physical
    # shortest rotation before classifying a key as an outlier.
    return min(angle, abs(360.0 - angle))


def _sanitize_sequence_rotation_keys(reader, sequence_name: str):
    """Repair unambiguous body corruption and quaternion sign discontinuities."""

    sequence = reader.sequences[sequence_name]
    bone_count = len(reader.bones)
    keys = reader.read_sequence_keys(sequence_name)
    rotations_by_bone = {}
    metrics = {}
    primary_outliers = set()
    primary_core_outliers_by_frame = {}

    for bone_index, bone in enumerate(reader.bones):
        bone_name = bone.name.decode("windows-1252").rstrip()
        if not _is_humanoid_body_bone(bone_name):
            continue

        rotations = []
        for frame_index in range(sequence.frame_count):
            key = keys[frame_index * bone_count + bone_index]
            location = (
                float(key.location.x),
                float(key.location.y),
                float(key.location.z),
            )
            if not all(math.isfinite(component) for component in location):
                raise RuntimeError(
                    f"PSA contains a non-finite location key on {bone_name} "
                    f"at source frame {frame_index}."
                )
            rotations.append(_normalized_rotation(key))
        rotations_by_bone[bone_index] = rotations
        for frame_index in range(1, sequence.frame_count - 1):
            previous = rotations[frame_index - 1]
            current = rotations[frame_index]
            following = rotations[frame_index + 1]
            incoming_angle = _rotation_angle_degrees(previous, current)
            outgoing_angle = _rotation_angle_degrees(current, following)
            neighbor_angle = _rotation_angle_degrees(previous, following)
            replacement = previous.slerp(following, 0.5)
            residual_angle = _rotation_angle_degrees(current, replacement)
            key_id = (bone_index, frame_index)
            metrics[key_id] = {
                "bone": bone_name,
                "sourceFrame": frame_index,
                "sourceTime": frame_index / float(sequence.fps),
                "incomingAngle": incoming_angle,
                "outgoingAngle": outgoing_angle,
                "neighborAngle": neighbor_angle,
                "residualAngle": residual_angle,
            }
            if (
                incoming_angle > _OUTLIER_JUMP_DEGREES
                and outgoing_angle > _OUTLIER_JUMP_DEGREES
                and neighbor_angle < _OUTLIER_NEIGHBOR_BRIDGE_DEGREES
            ):
                primary_outliers.add(key_id)
                if not _is_humanoid_finger_bone(bone_name):
                    primary_core_outliers_by_frame.setdefault(frame_index, []).append(
                        key_id
                    )
    correction_classifications = {key_id: "isolated-key" for key_id in primary_outliers}
    global_corrupt_frames = {
        frame_index
        for frame_index, key_ids in primary_core_outliers_by_frame.items()
        if len(key_ids) >= _GLOBAL_CORRUPT_FRAME_MIN_CORE_BONES
    }
    for frame_index in global_corrupt_frames:
        for bone_index in rotations_by_bone:
            key_id = (bone_index, frame_index)
            if (
                key_id in metrics
                and metrics[key_id]["residualAngle"]
                > _GLOBAL_CORRUPT_FRAME_MIN_RESIDUAL_DEGREES
            ):
                correction_classifications[key_id] = "corrupt-body-frame"

    # Detect against the untouched source tracks, then apply all corrections.
    # This prevents one repair from changing whether a neighboring key passes a
    # conservative outlier test.
    corrections = []
    for (bone_index, frame_index), classification in sorted(
        correction_classifications.items(), key=lambda item: (item[0][1], item[0][0])
    ):
        rotations = rotations_by_bone[bone_index]
        replacement = rotations[frame_index - 1].slerp(
            rotations[frame_index + 1], 0.5
        )
        key = keys[frame_index * bone_count + bone_index]
        key.rotation.w = replacement.w
        key.rotation.x = replacement.x
        key.rotation.y = replacement.y
        key.rotation.z = replacement.z
        corrections.append(
            {
                **metrics[(bone_index, frame_index)],
                "classification": classification,
            }
        )

    compatibility_flip_count = _make_quaternion_tracks_hemisphere_continuous(
        keys, bone_count, sequence.frame_count
    )
    return keys, corrections, compatibility_flip_count


def _make_quaternion_tracks_hemisphere_continuous(
    keys, bone_count: int, frame_count: int
) -> int:
    """Keep equivalent q/-q keys on one interpolation hemisphere per track.

    The pinned Blender PSA importer writes quaternion components to independent
    linear F-curves. A source sign change therefore becomes a long-path spin even
    though q and -q represent the same orientation. Flipping the representation
    when consecutive normalized quaternions have a negative dot product preserves
    every authored pose while making interpolation deterministic.
    """

    flip_count = 0
    for bone_index in range(bone_count):
        previous = _normalized_rotation(keys[bone_index])
        for frame_index in range(1, frame_count):
            key = keys[frame_index * bone_count + bone_index]
            current = _normalized_rotation(key)
            if previous.dot(current) < 0.0:
                key.rotation.w = -key.rotation.w
                key.rotation.x = -key.rotation.x
                key.rotation.y = -key.rotation.y
                key.rotation.z = -key.rotation.z
                current.negate()
                flip_count += 1
            previous = current

    return flip_count


class _SanitizedPsaReader:
    """Narrow adapter that keeps the pinned vendor importer unmodified."""

    def __init__(self, source_reader):
        self._source_reader = source_reader
        self._key_cache = {}
        self.corrections = {}
        self.quaternion_sign_corrections = {}

    @property
    def bones(self):
        return self._source_reader.bones

    @property
    def sequences(self):
        return self._source_reader.sequences

    @property
    def has_scale_keys(self):
        return self._source_reader.has_scale_keys

    def read_sequence_keys(self, sequence_name: str):
        if sequence_name not in self._key_cache:
            keys, corrections, sign_correction_count = _sanitize_sequence_rotation_keys(
                self._source_reader, sequence_name
            )
            self._key_cache[sequence_name] = keys
            self.corrections[sequence_name] = corrections
            self.quaternion_sign_corrections[sequence_name] = sign_correction_count
        return self._key_cache[sequence_name]

    def read_sequence_scale_keys(self, sequence_name: str):
        return self._source_reader.read_sequence_scale_keys(sequence_name)


def _parse_arguments() -> argparse.Namespace:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--report")
    parser.add_argument("--asset-path", action="append", default=[])
    parser.add_argument("--only-with-source-key-corrections", action="store_true")
    return parser.parse_args(argv)


def _load_pipeline(manifest_path: pathlib.Path):
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    sys.path.insert(0, manifest["pythonRoot"])
    sys.path.insert(0, manifest["addonRoot"])

    from io_scene_psk_psa.psa.config import PsaConfig, REMOVE_TRACK_LOCATION
    from io_scene_psk_psa.psa.importer import PsaImportOptions, import_psa
    from psk_psa_py.psa.reader import PsaReader

    return (
        manifest,
        PsaReader,
        PsaConfig,
        REMOVE_TRACK_LOCATION,
        PsaImportOptions,
        import_psa,
    )


def _load_armature(rig_path: str):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=rig_path)
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(
            f"Expected exactly one armature in {rig_path}, found {len(armatures)}"
        )

    armature = armatures[0]
    armature.name = "FrankHumanoidRig"
    armature.data.name = "FrankHumanoidRig"
    if armature.animation_data is not None:
        armature.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    return armature


def _filter_jobs_with_source_key_corrections(jobs: list[dict], PsaReader) -> list[dict]:
    filtered_jobs = []
    for index, job in enumerate(jobs, start=1):
        psa_path = pathlib.Path(job["psa"])
        with psa_path.open("rb") as stream:
            reader = _SanitizedPsaReader(PsaReader(stream))
            sequence_names = list(reader.sequences.keys())
            if len(sequence_names) != 1:
                raise RuntimeError(
                    f"Expected one sequence in {psa_path}, found {sequence_names}"
                )
            sequence_name = sequence_names[0]
            reader.read_sequence_keys(sequence_name)
            if reader.corrections[sequence_name]:
                filtered_jobs.append(job)
        if index % 100 == 0 or index == len(jobs):
            print(
                f"FRANK_KEY_AUDIT [{index}/{len(jobs)}] "
                f"{len(filtered_jobs)} clips require repair"
            )
    return filtered_jobs


def _select_only(armature) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    armature.hide_set(False)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature


def _export_rig(armature, output_path: pathlib.Path) -> None:
    if armature.animation_data is not None:
        armature.animation_data_clear()

    output_path.parent.mkdir(parents=True, exist_ok=True)
    _select_only(armature)
    result = bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=True,
        object_types={"ARMATURE"},
        use_custom_props=False,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False,
        use_armature_deform_only=False,
        armature_nodetype="NULL",
        bake_anim=False,
    )
    if result != {"FINISHED"} or not output_path.is_file():
        raise RuntimeError(f"Blender failed to export shared rig {output_path}: {result}")


def _export_action(armature, action, target_fps: int, output_path: pathlib.Path) -> None:
    animation_data = armature.animation_data_create()
    animation_data.action = action
    if len(action.slots) > 0:
        animation_data.action_slot = action.slots[0]

    start_frame = int(round(action.frame_range[0]))
    end_frame = int(round(action.frame_range[1]))
    if end_frame <= start_frame:
        raise RuntimeError(
            f"Animation {action.name} has invalid frame range {action.frame_range[:]}."
        )

    scene = bpy.context.scene
    scene.frame_start = start_frame
    scene.frame_end = end_frame
    scene.render.fps = target_fps
    scene.frame_set(start_frame)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    _select_only(armature)
    result = bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=True,
        object_types={"ARMATURE"},
        use_custom_props=False,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False,
        use_armature_deform_only=False,
        armature_nodetype="NULL",
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
    )
    if result != {"FINISHED"} or not output_path.is_file():
        raise RuntimeError(f"Blender failed to export {output_path}: {result}")


def _convert_job(
    armature,
    job: dict,
    PsaReader,
    PsaConfig,
    REMOVE_TRACK_LOCATION,
    PsaImportOptions,
    import_psa,
) -> dict:
    psa_path = pathlib.Path(job["psa"])
    output_path = pathlib.Path(job["output"])
    if output_path.exists() and not job.get("overwrite", True):
        return {**job, "status": "skipped"}

    with psa_path.open("rb") as stream:
        reader = _SanitizedPsaReader(PsaReader(stream))
        sequence_names = list(reader.sequences.keys())
        if len(sequence_names) != 1:
            raise RuntimeError(
                f"Expected one sequence in {psa_path}, found {sequence_names}"
            )

        sequence_name = sequence_names[0]
        sequence = reader.sequences[sequence_name]
        target_fps = max(1, min(60, int(round(float(sequence.fps)))))
        psa_config = PsaConfig()
        psa_config.sequence_bone_flags[sequence_name] = {
            index: REMOVE_TRACK_LOCATION
            for index, bone in enumerate(reader.bones)
            if bone.name.decode("windows-1252").rstrip().lower()
            not in {"root", "pelvis"}
        }
        options = PsaImportOptions(
            psa_config=psa_config,
            sequence_names=[sequence_name],
            # Keep the source samples intact inside Blender. Resampling a 60.22
            # FPS PSA to 60 FPS can choose a new sign independently for adjacent
            # quaternion samples and undo the hemisphere-continuity pass above.
            # FBX playback rate is still normalized to target_fps during export.
            fps_custom=float(sequence.fps),
            fps_source="SEQUENCE",
            should_overwrite=True,
            should_use_config_file=False,
            should_write_metadata=False,
            should_write_scale_keys=False,
            translation_scale=1.0,
        )
        result = import_psa(bpy.context, reader, armature, options)
        action = bpy.data.actions.get(sequence_name)
        if action is None:
            raise RuntimeError(f"PSA importer did not create action {sequence_name}")

        _export_action(armature, action, target_fps, output_path)
        frame_count = int(round(action.frame_range[1] - action.frame_range[0])) + 1
        bone_names = {
            bone.name.decode("windows-1252").rstrip() for bone in reader.bones
        }
        mapped_bones = len(bone_names.intersection(armature.data.bones.keys()))
        record = {
            **job,
            "status": "converted",
            "sequence": sequence_name,
            "frames": frame_count,
            "fps": float(target_fps),
            "sourceFps": float(sequence.fps),
            "sourceBones": len(bone_names),
            "mappedBones": mapped_bones,
            "warnings": result.warnings,
            "sourceKeyCorrectionCount": len(reader.corrections[sequence_name]),
            "sourceKeyCorrections": reader.corrections[sequence_name],
            "quaternionSignCorrectionCount": reader.quaternion_sign_corrections[
                sequence_name
            ],
            "bytes": output_path.stat().st_size,
        }

    armature.animation_data.action = None
    bpy.data.actions.remove(action)
    return record


def main() -> int:
    args = _parse_arguments()
    manifest_path = pathlib.Path(args.manifest).resolve()
    (
        manifest,
        PsaReader,
        PsaConfig,
        REMOVE_TRACK_LOCATION,
        PsaImportOptions,
        import_psa,
    ) = _load_pipeline(manifest_path)
    if args.report:
        manifest["report"] = str(pathlib.Path(args.report).resolve())
    if args.asset_path:
        selected_paths = {path.replace("\\", "/") for path in args.asset_path}
        manifest["jobs"] = [
            job
            for job in manifest["jobs"]
            if job["assetPath"].replace("\\", "/") in selected_paths
        ]
        missing_paths = selected_paths.difference(
            job["assetPath"].replace("\\", "/") for job in manifest["jobs"]
        )
        if missing_paths:
            raise RuntimeError(
                "Requested animation assets are missing from the manifest: "
                + ", ".join(sorted(missing_paths))
            )
    if args.only_with_source_key_corrections:
        manifest["jobs"] = _filter_jobs_with_source_key_corrections(
            manifest["jobs"], PsaReader
        )
        print(
            "FRANK_KEY_AUDIT COMPLETE: "
            f"{len(manifest['jobs'])} clips contain unambiguous source key outliers."
        )
    armature = _load_armature(manifest["rigFbx"])
    if (
        manifest.get("rigOutput")
        and not args.only_with_source_key_corrections
        and not args.asset_path
    ):
        _export_rig(armature, pathlib.Path(manifest["rigOutput"]))
    reports = []

    for index, job in enumerate(manifest["jobs"], start=1):
        try:
            report = _convert_job(
                armature,
                job,
                PsaReader,
                PsaConfig,
                REMOVE_TRACK_LOCATION,
                PsaImportOptions,
                import_psa,
            )
            print(
                f"FRANK_IMPORT [{index}/{len(manifest['jobs'])}] "
                f"{report['status']}: {job['output']}"
            )
            reports.append(report)
        except Exception as exception:
            reports.append(
                {
                    **job,
                    "status": "failed",
                    "error": str(exception),
                    "traceback": traceback.format_exc(),
                }
            )
            print(f"FRANK_IMPORT FAILED: {job['psa']}: {exception}", file=sys.stderr)

    report_path = pathlib.Path(manifest["report"])
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(
        json.dumps(reports, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    failures = sum(item["status"] == "failed" for item in reports)
    print(
        f"FRANK_IMPORT COMPLETE: {len(reports) - failures} succeeded, "
        f"{failures} failed. Report: {report_path}"
    )
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
