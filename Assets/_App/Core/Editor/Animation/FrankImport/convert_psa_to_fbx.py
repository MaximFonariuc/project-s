"""Convert UE Viewer PSA sequences to Unity-compatible Humanoid FBX files.

Each Frank source set has its own bind pose. The converter reconstructs a
Humanoid-compatible armature from the reference transform embedded in every
PSA instead of applying every sequence to one unrelated shared rig. Source
keys are validated and quaternion signs may be normalized to the same
hemisphere. A deterministic local-axis normalization is applied to both calf
bones because Unreal and Unity Humanoid interpret their longitudinal roll in
opposite directions; positions and the authored motion are otherwise kept.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import pathlib
import sys
import traceback

import bpy
from bpy_extras import anim_utils
from mathutils import Matrix, Quaternion, Vector


_BODY_PARENT_ITEMS = (
    ("root", None),
    ("pelvis", "root"),
    ("thigh_l", "pelvis"),
    ("calf_l", "thigh_l"),
    ("foot_l", "calf_l"),
    ("ball_l", "foot_l"),
    ("thigh_r", "pelvis"),
    ("calf_r", "thigh_r"),
    ("foot_r", "calf_r"),
    ("ball_r", "foot_r"),
    ("spine_01", "pelvis"),
    ("spine_02", "spine_01"),
    ("spine_03", "spine_02"),
    ("neck_01", "spine_03"),
    ("head", "neck_01"),
    ("clavicle_l", "spine_03"),
    ("upperarm_l", "clavicle_l"),
    ("lowerarm_l", "upperarm_l"),
    ("hand_l", "lowerarm_l"),
    ("clavicle_r", "spine_03"),
    ("upperarm_r", "clavicle_r"),
    ("lowerarm_r", "upperarm_r"),
    ("hand_r", "lowerarm_r"),
    ("thumb_01_l", "hand_l"),
    ("thumb_02_l", "thumb_01_l"),
    ("thumb_03_l", "thumb_02_l"),
    ("index_01_l", "hand_l"),
    ("index_02_l", "index_01_l"),
    ("index_03_l", "index_02_l"),
    ("middle_01_l", "hand_l"),
    ("middle_02_l", "middle_01_l"),
    ("middle_03_l", "middle_02_l"),
    ("ring_01_l", "hand_l"),
    ("ring_02_l", "ring_01_l"),
    ("ring_03_l", "ring_02_l"),
    ("pinky_01_l", "hand_l"),
    ("pinky_02_l", "pinky_01_l"),
    ("pinky_03_l", "pinky_02_l"),
    ("thumb_01_r", "hand_r"),
    ("thumb_02_r", "thumb_01_r"),
    ("thumb_03_r", "thumb_02_r"),
    ("index_01_r", "hand_r"),
    ("index_02_r", "index_01_r"),
    ("index_03_r", "index_02_r"),
    ("middle_01_r", "hand_r"),
    ("middle_02_r", "middle_01_r"),
    ("middle_03_r", "middle_02_r"),
    ("ring_01_r", "hand_r"),
    ("ring_02_r", "ring_01_r"),
    ("ring_03_r", "ring_02_r"),
    ("pinky_01_r", "hand_r"),
    ("pinky_02_r", "pinky_01_r"),
    ("pinky_03_r", "pinky_02_r"),
)

_REQUIRED_BODY_BONES = {
    "root",
    "pelvis",
    "thigh_l",
    "calf_l",
    "foot_l",
    "thigh_r",
    "calf_r",
    "foot_r",
    "spine_01",
    "spine_02",
    "spine_03",
    "neck_01",
    "head",
    "upperarm_l",
    "lowerarm_l",
    "hand_l",
    "upperarm_r",
    "lowerarm_r",
    "hand_r",
}

_MAX_BIND_ROTATION_ERROR_DEGREES = 0.01
_MAX_BIND_LOCATION_ERROR = 0.01
_REFERENCE_POSE_FRAME = 0.0
_AUTHORED_ANIMATION_FRAME_OFFSET = 1.0
_CALF_ROLL_BONES = ("calf_l", "calf_r")
_CALF_ROLL_CORRECTION = Quaternion((1.0, 0.0, 0.0), math.pi)


def _parse_arguments() -> argparse.Namespace:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--report")
    parser.add_argument("--asset-path", action="append", default=[])
    return parser.parse_args(argv)


def _load_pipeline(manifest_path: pathlib.Path):
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    sys.path.insert(0, manifest["pythonRoot"])
    sys.path.insert(0, manifest["addonRoot"])

    from io_scene_psk_psa.psa.config import PsaConfig
    from io_scene_psk_psa.psa.importer import (
        ImportBone,
        PsaImportOptions,
        _calculate_fcurve_data,
        import_psa,
    )
    from psk_psa_py.psa.reader import PsaReader

    return (
        manifest,
        PsaReader,
        PsaConfig,
        ImportBone,
        PsaImportOptions,
        _calculate_fcurve_data,
        import_psa,
    )


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


def _validate_and_normalize_keys(keys, bone_count: int, frame_count: int) -> int:
    """Validate source samples and normalize only equivalent q/-q notation."""

    flip_count = 0
    for bone_index in range(bone_count):
        previous = None
        for frame_index in range(frame_count):
            key = keys[frame_index * bone_count + bone_index]
            location = (
                float(key.location.x),
                float(key.location.y),
                float(key.location.z),
            )
            if not all(math.isfinite(component) for component in location):
                raise RuntimeError("PSA contains a non-finite location key.")

            current = _normalized_rotation(key)
            if previous is not None and previous.dot(current) < 0.0:
                key.rotation.w = -key.rotation.w
                key.rotation.x = -key.rotation.x
                key.rotation.y = -key.rotation.y
                key.rotation.z = -key.rotation.z
                current.negate()
                flip_count += 1
            previous = current
    return flip_count


class _PosePreservingPsaReader:
    def __init__(self, source_reader):
        self._source_reader = source_reader
        self._key_cache = {}
        self.quaternion_hemisphere_flips = {}

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
            sequence = self.sequences[sequence_name]
            keys = self._source_reader.read_sequence_keys(sequence_name)
            flips = _validate_and_normalize_keys(
                keys, len(self.bones), sequence.frame_count
            )
            self._key_cache[sequence_name] = keys
            self.quaternion_hemisphere_flips[sequence_name] = flips
        return self._key_cache[sequence_name]

    def read_sequence_scale_keys(self, sequence_name: str):
        keys = self._source_reader.read_sequence_scale_keys(sequence_name)
        for key in keys:
            if not all(math.isfinite(float(value)) for value in key.scale):
                raise RuntimeError("PSA contains a non-finite scale key.")
        return keys


def _source_bones_by_name(reader) -> dict:
    result = {}
    for bone in reader.bones:
        name = bone.name.decode("windows-1252").rstrip()
        if name in result:
            raise RuntimeError(f"PSA contains duplicate bone name: {name}")
        result[name] = bone
    missing = sorted(_REQUIRED_BODY_BONES.difference(result))
    if missing:
        raise RuntimeError("PSA is missing required Humanoid bones: " + ", ".join(missing))
    return result


def _body_records(reader) -> list[tuple[str, str | None, object]]:
    source_bones = _source_bones_by_name(reader)
    records = []
    included = set()
    for name, parent_name in _BODY_PARENT_ITEMS:
        if name not in source_bones:
            continue
        if parent_name is not None and parent_name not in included:
            if name in _REQUIRED_BODY_BONES:
                raise RuntimeError(
                    f"PSA Humanoid hierarchy cannot attach {name} to {parent_name}."
                )
            continue
        records.append((name, parent_name, source_bones[name]))
        included.add(name)
    return records


def _skeleton_fingerprint(records) -> str:
    payload = []
    for name, parent_name, bone in records:
        payload.append(
            {
                "name": name,
                "parent": parent_name,
                "rotation": [round(float(value), 7) for value in bone.rotation],
                "location": [round(float(value), 6) for value in bone.location],
            }
        )
    serialized = json.dumps(
        payload, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    return hashlib.sha256(serialized).hexdigest()


def _create_source_armature(reader):
    """Recreate the source bind pose using the PSK/PSA coordinate contract."""

    bpy.ops.wm.read_factory_settings(use_empty=True)
    records = _body_records(reader)
    armature_data = bpy.data.armatures.new("FrankSourceRig")
    armature = bpy.data.objects.new("FrankSourceRig", armature_data)
    bpy.context.scene.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    for name, _, _ in records:
        armature_data.edit_bones.new(name)

    transforms = {}
    for name, parent_name, source_bone in records:
        local_rotation = Quaternion(tuple(source_bone.rotation))
        local_rotation.normalize()
        local_translation = Vector(tuple(source_bone.location))
        if parent_name is None:
            world_rotation = local_rotation
            world_position = local_translation
        else:
            parent = transforms[parent_name]
            translation = local_translation.copy()
            translation.rotate(parent["world_rotation"])
            world_position = parent["world_position"] + translation
            world_rotation = parent["world_rotation"] @ local_rotation

        edit_bone = armature_data.edit_bones[name]
        if parent_name is not None:
            edit_bone.parent = armature_data.edit_bones[parent_name]
        edit_bone.tail = Vector((0.0, 4.0, 0.0))
        edit_bone_matrix = world_rotation.to_matrix().to_4x4()
        edit_bone_matrix.translation = world_position
        edit_bone.matrix = edit_bone_matrix
        transforms[name] = {
            "world_rotation": world_rotation,
            "world_position": world_position,
        }

    bpy.ops.object.mode_set(mode="OBJECT")
    return armature, records


def _validate_bind_pose(
    armature,
    records,
    ImportBone,
    calculate_fcurve_data,
) -> tuple[float, float]:
    max_rotation_error = 0.0
    max_location_error = 0.0
    worst_rotation_bone = ""
    worst_location_bone = ""
    for name, _, source_bone in records:
        armature_bone = armature.data.bones[name]
        import_bone = ImportBone(source_bone)
        import_bone.armature_bone = armature_bone
        if armature_bone.parent is not None:
            import_bone.original_location = (
                armature_bone.matrix_local.translation
                - armature_bone.parent.matrix_local.translation
            )
            import_bone.original_location.rotate(
                armature_bone.parent.matrix_local.to_quaternion().conjugated()
            )
            import_bone.original_rotation = armature_bone.matrix_local.to_quaternion()
            import_bone.original_rotation.rotate(
                armature_bone.parent.matrix_local.to_quaternion().conjugated()
            )
            import_bone.original_rotation.conjugate()
        else:
            import_bone.original_location = armature_bone.matrix_local.translation.copy()
            import_bone.original_rotation = (
                armature_bone.matrix_local.to_quaternion().conjugated()
            )
        import_bone.post_rotation = import_bone.original_rotation.conjugated()

        source_data = [
            *[float(value) for value in source_bone.rotation],
            *[float(value) for value in source_bone.location],
            1.0,
            1.0,
            1.0,
        ]
        result = calculate_fcurve_data(import_bone, source_data)
        rotation = Quaternion(result[:4]).normalized()
        rotation_error = math.degrees(
            min(rotation.angle, abs((2.0 * math.pi) - rotation.angle))
        )
        location_error = Vector(result[4:7]).length
        if rotation_error > max_rotation_error:
            max_rotation_error = rotation_error
            worst_rotation_bone = name
        if location_error > max_location_error:
            max_location_error = location_error
            worst_location_bone = name

    if max_rotation_error > _MAX_BIND_ROTATION_ERROR_DEGREES:
        raise RuntimeError(
            f"Reconstructed bind pose rotation error is {max_rotation_error:.6f} degrees "
            f"on {worst_rotation_bone}."
        )
    if max_location_error > _MAX_BIND_LOCATION_ERROR:
        raise RuntimeError(
            f"Reconstructed bind pose location error is {max_location_error:.6f} "
            f"on {worst_location_bone}."
        )
    return max_rotation_error, max_location_error


def _select_only(armature) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    armature.hide_set(False)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature


def _prepend_reference_pose(armature, action) -> None:
    """Keep the FBX skeleton bind pose separate from the first authored pose.

    Blender's FBX exporter uses the pose evaluated at the first exported frame
    as the node defaults.  Exporting a PSA action from frame zero therefore made
    Unity compare the source Avatar bind pose against the first attack frame.
    Shift authored keys by one frame and reserve frame zero for pose-basis
    identity so Copy From Other Avatar sees an identical skeleton definition.
    """

    animation_data = armature.animation_data_create()
    animation_data.action = action
    if len(action.slots) > 0:
        animation_data.action_slot = action.slots[0]

    channelbag = anim_utils.action_get_channelbag_for_slot(
        action, animation_data.action_slot
    )
    if channelbag is None:
        raise RuntimeError(f"Animation {action.name} has no action channel bag.")
    for fcurve in channelbag.fcurves:
        for key in fcurve.keyframe_points:
            key.co.x += _AUTHORED_ANIMATION_FRAME_OFFSET
            key.handle_left.x += _AUTHORED_ANIMATION_FRAME_OFFSET
            key.handle_right.x += _AUTHORED_ANIMATION_FRAME_OFFSET
        fcurve.update()

    bpy.context.scene.frame_set(int(_REFERENCE_POSE_FRAME))
    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.location = Vector((0.0, 0.0, 0.0))
        pose_bone.rotation_quaternion = Quaternion((1.0, 0.0, 0.0, 0.0))
        pose_bone.scale = Vector((1.0, 1.0, 1.0))
        pose_bone.keyframe_insert(
            data_path="location", frame=_REFERENCE_POSE_FRAME, group=pose_bone.name
        )
        pose_bone.keyframe_insert(
            data_path="rotation_quaternion",
            frame=_REFERENCE_POSE_FRAME,
            group=pose_bone.name,
        )
        pose_bone.keyframe_insert(
            data_path="scale", frame=_REFERENCE_POSE_FRAME, group=pose_bone.name
        )


def _normalize_unreal_calf_roll(armature, authored_frame_count: int) -> int:
    """Convert Unreal calf roll to the local basis expected by Unity Humanoid.

    The correction is intentionally applied only to authored frames. Frame zero
    remains the unmodified source bind pose used by Unity when matching the
    copied Avatar. Post-multiplication rotates around each calf's own X axis;
    it does not change the knee trajectory or any joint translation.
    """

    corrected_samples = 0
    scene = bpy.context.scene
    for bone_name in _CALF_ROLL_BONES:
        pose_bone = armature.pose.bones.get(bone_name)
        if pose_bone is None:
            raise RuntimeError(f"PSA is missing calf bone required by Unity: {bone_name}")
        pose_bone.rotation_mode = "QUATERNION"
        for frame in range(1, authored_frame_count + 1):
            scene.frame_set(frame)
            pose_bone.rotation_quaternion = (
                pose_bone.rotation_quaternion.copy() @ _CALF_ROLL_CORRECTION
            )
            pose_bone.keyframe_insert(
                data_path="rotation_quaternion", frame=frame, group=pose_bone.name
            )
            corrected_samples += 1
    return corrected_samples


def _export_fbx(
    armature,
    output_path: pathlib.Path,
    *,
    bake_animation: bool,
    target_fps: int = 60,
) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    _select_only(armature)
    if bake_animation:
        action = armature.animation_data.action
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
        bake_anim=bake_animation,
        bake_anim_use_all_bones=bake_animation,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=bake_animation,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
    )
    if result != {"FINISHED"} or not output_path.is_file():
        raise RuntimeError(f"Blender failed to export {output_path}: {result}")


def _convert_job(
    manifest,
    emitted_rigs: set[str],
    job: dict,
    PsaReader,
    PsaConfig,
    ImportBone,
    PsaImportOptions,
    calculate_fcurve_data,
    import_psa,
) -> dict:
    psa_path = pathlib.Path(job["psa"])
    output_path = pathlib.Path(job["output"])
    with psa_path.open("rb") as stream:
        reader = _PosePreservingPsaReader(PsaReader(stream))
        sequence_names = list(reader.sequences.keys())
        if len(sequence_names) != 1:
            raise RuntimeError(
                f"Expected one sequence in {psa_path}, found {sequence_names}"
            )
        sequence_name = sequence_names[0]
        sequence = reader.sequences[sequence_name]
        target_fps = max(1, min(60, int(round(float(sequence.fps)))))
        armature, records = _create_source_armature(reader)
        fingerprint = _skeleton_fingerprint(records)
        max_rotation_error, max_location_error = _validate_bind_pose(
            armature, records, ImportBone, calculate_fcurve_data
        )

        rig_file_name = f"FrankSourceRig_{fingerprint[:12]}.fbx"
        rig_output = pathlib.Path(manifest["rigOutputRoot"]) / rig_file_name
        rig_asset_path = manifest["rigAssetRoot"].rstrip("/") + "/" + rig_file_name
        if fingerprint not in emitted_rigs:
            if armature.animation_data is not None:
                armature.animation_data_clear()
            _export_fbx(armature, rig_output, bake_animation=False)
            emitted_rigs.add(fingerprint)

        psa_config = PsaConfig()
        options = PsaImportOptions(
            psa_config=psa_config,
            sequence_names=[sequence_name],
            fps_custom=float(target_fps),
            fps_source="CUSTOM",
            should_overwrite=True,
            should_use_config_file=False,
            should_write_metadata=False,
            should_write_scale_keys=reader.has_scale_keys,
            translation_scale=1.0,
        )
        result = import_psa(bpy.context, reader, armature, options)
        action = bpy.data.actions.get(sequence_name)
        if action is None:
            raise RuntimeError(f"PSA importer did not create action {sequence_name}")
        _prepend_reference_pose(armature, action)
        calf_roll_correction_samples = _normalize_unreal_calf_roll(
            armature, int(sequence.frame_count)
        )
        animation_data = armature.animation_data_create()
        animation_data.action = action
        if len(action.slots) > 0:
            animation_data.action_slot = action.slots[0]
        _export_fbx(
            armature, output_path, bake_animation=True, target_fps=target_fps
        )

        source_names = {
            bone.name.decode("windows-1252").rstrip() for bone in reader.bones
        }
        record = {
            **job,
            "status": "converted",
            "sequence": sequence_name,
            "sourceFrames": int(sequence.frame_count),
            "frames": int(round(action.frame_range[1] - action.frame_range[0])) + 1,
            "fps": float(target_fps),
            "sourceFps": float(sequence.fps),
            "durationSeconds": float(sequence.frame_count) / float(sequence.fps),
            "referencePoseFrames": 1,
            "authoredAnimationStartFrame": 1,
            "authoredAnimationEndFrame": int(sequence.frame_count),
            "resampledToTargetFps": abs(float(sequence.fps) - target_fps) > 1e-4,
            "sourceBones": len(source_names),
            "bodyBones": len(records),
            "mappedBones": len(source_names.intersection(armature.data.bones.keys())),
            "skeletonFingerprint": fingerprint,
            "rigAssetPath": rig_asset_path,
            "bindPoseMaxRotationErrorDegrees": max_rotation_error,
            "bindPoseMaxLocationError": max_location_error,
            "sourceTransformMutationCount": calf_roll_correction_samples,
            "calfRollCorrectionAxis": "localX",
            "calfRollCorrectionDegrees": 180,
            "quaternionHemisphereFlipCount": reader.quaternion_hemisphere_flips[
                sequence_name
            ],
            "warnings": result.warnings,
            "bytes": output_path.stat().st_size,
        }
    return record


def main() -> int:
    args = _parse_arguments()
    manifest_path = pathlib.Path(args.manifest).resolve()
    (
        manifest,
        PsaReader,
        PsaConfig,
        ImportBone,
        PsaImportOptions,
        calculate_fcurve_data,
        import_psa,
    ) = _load_pipeline(manifest_path)
    if args.report:
        manifest["report"] = str(pathlib.Path(args.report).resolve())
    if args.asset_path:
        selected = {path.replace("\\", "/") for path in args.asset_path}
        manifest["jobs"] = [
            job
            for job in manifest["jobs"]
            if job["assetPath"].replace("\\", "/") in selected
        ]
        found = {job["assetPath"].replace("\\", "/") for job in manifest["jobs"]}
        missing = selected.difference(found)
        if missing:
            raise RuntimeError(
                "Requested animation assets are missing from the manifest: "
                + ", ".join(sorted(missing))
            )

    reports = []
    emitted_rigs = set()
    for index, job in enumerate(manifest["jobs"], start=1):
        try:
            report = _convert_job(
                manifest,
                emitted_rigs,
                job,
                PsaReader,
                PsaConfig,
                ImportBone,
                PsaImportOptions,
                calculate_fcurve_data,
                import_psa,
            )
            reports.append(report)
            print(
                f"FRANK_IMPORT [{index}/{len(manifest['jobs'])}] "
                f"converted: {job['output']}"
            )
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
