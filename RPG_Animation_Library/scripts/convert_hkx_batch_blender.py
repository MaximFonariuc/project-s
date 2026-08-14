#!/usr/bin/env python3
"""Convert a resumable batch of unpacked ER player HKX files in one Blender process."""

from __future__ import annotations

import argparse
import json
import sys
import traceback
from pathlib import Path
from types import SimpleNamespace

import bpy
import gpu

sys.path.insert(0, str(Path(__file__).resolve().parent))
from convert_hkx_blender import PipelineLog, collect_motion_metadata


class FileBinderEntry:
    """Minimal BinderEntry surface required by Soulstruct's HKX reader."""

    def __init__(self, path: Path):
        self.path = path
        self.name = path.name

    def get_uncompressed_data(self) -> bytes:
        return self.path.read_bytes()


def parse_args() -> argparse.Namespace:
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--results", required=True, type=Path)
    return parser.parse_args(arguments)


def clear_scene() -> None:
    if bpy.context.mode != "OBJECT" and bpy.ops.object.mode_set.poll():
        bpy.ops.object.mode_set(mode="OBJECT", toggle=False)
    if bpy.ops.object.select_all.poll():
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.object.delete(use_global=False)
    for collection in tuple(bpy.data.collections):
        bpy.data.collections.remove(collection)
    for action in tuple(bpy.data.actions):
        bpy.data.actions.remove(action)
    for armature in tuple(bpy.data.armatures):
        bpy.data.armatures.remove(armature)


def configure_scene() -> None:
    scene = bpy.context.scene
    scene.soulstruct_settings.game_enum = "ELDEN_RING"
    scene.flver_import_settings.import_textures = False
    scene.animation_import_settings.to_60_fps = False
    scene.render.fps = 30


def create_source_armature(flver, bone_names: list[str]):
    from soulstruct.blender.flver.models.types.bl_flver._import_bones import (
        create_edit_bones,
        write_flver_rest_pose_to_edit_bones,
    )

    scene = bpy.context.scene
    armature_data = bpy.data.armatures.new(f"{flver.path.stem} Armature")
    armature = bpy.data.objects.new(flver.path.stem, armature_data)
    scene.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT", toggle=False)
    edit_bones = create_edit_bones(flver, armature_data, bone_names)
    write_flver_rest_pose_to_edit_bones(PipelineLog(), flver, edit_bones)
    del edit_bones
    bpy.ops.object.mode_set(mode="OBJECT", toggle=False)
    return armature


def main() -> int:
    args = parse_args()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    for key in ("flver", "baseAnibnd", "compendium"):
        path = Path(manifest[key])
        if not path.is_file():
            raise FileNotFoundError(path)

    gpu.init()
    bpy.ops.preferences.addon_enable(module="io_soulstruct")

    from soulstruct.blender.animation.types import SoulstructAnimation
    from soulstruct.blender.animation.utilities import read_animation_hkx_entry, read_skeleton_hkx_entry
    from soulstruct.eldenring.containers import DivBinder
    from soulstruct.flver import FLVER
    from soulstruct.havok.core import HKX

    flver_path = Path(manifest["flver"])
    flver = FLVER.from_path(flver_path)
    flver.path = flver_path
    bone_names: list[str] = []
    for bone in flver.bones:
        bone_name = f"{bone.name} <DUPE>" if bone.name in bone_names else bone.name
        bone_names.append(bone_name)

    base_binder = DivBinder.from_path(Path(manifest["baseAnibnd"]))
    skeleton_entry = base_binder.find_entry_matching_name(r"Skeleton\.hkx(\.dcx)?$")
    compendium = HKX.from_path(Path(manifest["compendium"]))
    skeleton_hkx = read_skeleton_hkx_entry(skeleton_entry, compendium)

    results: list[dict] = []
    for item in manifest["animations"]:
        animation_id = item["animationId"]
        try:
            hkx_path = Path(item["hkxPath"])
            output_blend = Path(item["outputBlend"])
            output_metadata = Path(item["outputMetadata"])
            if not hkx_path.is_file():
                raise FileNotFoundError(hkx_path)

            clear_scene()
            configure_scene()
            armature = create_source_armature(flver, bone_names)
            animation_hkx = read_animation_hkx_entry(FileBinderEntry(hkx_path), compendium)
            blend_hint = int(animation_hkx.animation_container.hkx_binding.blendHint)
            if blend_hint != 0:
                raise RuntimeError(
                    f"Additive HKX requires a reference pose and cannot use the standalone retarget path: "
                    f"blendHint={blend_hint}"
                )
            animation = SoulstructAnimation.new_from_hkx_animation(
                PipelineLog(),
                bpy.context,
                animation_hkx,
                skeleton_hkx,
                name=animation_id,
                armature_obj=armature,
                model_name=flver_path.stem,
            )
            action = animation.action
            scene = bpy.context.scene
            scene.frame_start = int(round(action.frame_range[0]))
            scene.frame_end = int(round(action.frame_range[1]))
            armature["pipelineAnimationId"] = animation_id
            armature["pipelineSourceEntry"] = hkx_path.name
            armature["pipelineCoordinateSpace"] = "BlenderZUp_SourceNative"
            scene["pipelineAnimationId"] = animation_id
            scene["pipelinePhase"] = "Phase5HKXBlenderBatch"

            motion = collect_motion_metadata(armature, action)
            output_blend.parent.mkdir(parents=True, exist_ok=True)
            output_metadata.parent.mkdir(parents=True, exist_ok=True)
            save_result = bpy.ops.wm.save_as_mainfile(filepath=str(output_blend), check_existing=False)
            if save_result != {"FINISHED"}:
                raise RuntimeError(f"Blender save failed: {save_result}")

            metadata = {
                "schemaVersion": 1,
                "sourceFlver": str(flver_path),
                "sourceBaseAnibnd": str(Path(manifest["baseAnibnd"])),
                "sourceAnimationAnibnd": str(Path(manifest["animationAnibnd"])),
                "sourceAnimationId": animation_id,
                "sourceAnimationEntry": hkx_path.name,
                "blenderVersion": bpy.app.version_string,
                "blenderPath": str(output_blend),
                "coordinateSpace": "BlenderZUp_SourceNative",
                "blendHint": blend_hint,
                **motion,
            }
            output_metadata.write_text(
                json.dumps(metadata, ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8",
            )
            results.append(
                {
                    "animationId": animation_id,
                    "status": "PASS",
                    "outputBlend": str(output_blend),
                    "outputMetadata": str(output_metadata),
                    "motionFingerprintSHA256": metadata["motionFingerprintSHA256"],
                }
            )
            print(f"BATCH_CONVERSION_PASS:{animation_id};frames={metadata['frameCount']}")
        except Exception as exception:
            traceback.print_exc()
            results.append(
                {
                    "animationId": animation_id,
                    "status": "FAIL",
                    "error": f"{type(exception).__name__}: {exception}",
                }
            )
            print(f"BATCH_CONVERSION_FAIL:{animation_id}:{type(exception).__name__}:{exception}")

        args.results.parent.mkdir(parents=True, exist_ok=True)
        args.results.write_text(
            json.dumps({"schemaVersion": 1, "results": results}, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    passed = sum(item["status"] == "PASS" for item in results)
    failed = len(results) - passed
    print(f"BATCH_CONVERSION_COMPLETE:passed={passed};failed={failed}")
    return 0 if failed == 0 else 2


if __name__ == "__main__":
    raise SystemExit(main())
