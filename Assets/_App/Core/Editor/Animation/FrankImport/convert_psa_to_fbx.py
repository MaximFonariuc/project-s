"""Batch-convert UE Viewer PSA files to Unity-ready FBX animation files.

The script is executed by Blender in background mode. It deliberately uses the
validated Frank dual-sword armature as the common rest-pose contract so every
body clip has an identical hierarchy before Unity performs Humanoid retargeting.
"""

from __future__ import annotations

import argparse
import json
import pathlib
import sys
import traceback

import bpy


def _parse_arguments() -> argparse.Namespace:
    argv = sys.argv
    argv = argv[argv.index("--") + 1 :] if "--" in argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True)
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
        reader = PsaReader(stream)
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
            fps_custom=target_fps,
            fps_source="CUSTOM",
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
    armature = _load_armature(manifest["rigFbx"])
    if manifest.get("rigOutput"):
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
