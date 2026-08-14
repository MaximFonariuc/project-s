#!/usr/bin/env python3
"""Inspect Havok binding/track metadata for unpacked animation HKX files."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import bpy
import gpu

sys.path.insert(0, str(Path(__file__).resolve().parent))
from convert_hkx_batch_blender import FileBinderEntry


def parse_args() -> argparse.Namespace:
    arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compendium", required=True, type=Path)
    sources = parser.add_mutually_exclusive_group(required=True)
    sources.add_argument("--hkx", nargs="+", type=Path)
    sources.add_argument("--hkx-root", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args(arguments)


def public_values(value) -> dict:
    result = {}
    for name in dir(value):
        if name.startswith("_"):
            continue
        try:
            member = getattr(value, name)
        except Exception:
            continue
        if callable(member):
            continue
        if isinstance(member, (str, int, float, bool)) or member is None:
            result[name] = member
        elif isinstance(member, (list, tuple)):
            result[name] = {
                "count": len(member),
                "head": [str(item) for item in member[:8]],
            }
        else:
            result[name] = str(member)
    return result


def main() -> int:
    args = parse_args()
    gpu.init()
    bpy.ops.preferences.addon_enable(module="io_soulstruct")
    from soulstruct.blender.animation.utilities import read_animation_hkx_entry
    from soulstruct.havok.core import HKX

    compendium = HKX.from_path(args.compendium)
    hkx_paths = args.hkx or sorted(args.hkx_root.rglob("a*.hkx"))
    compact = args.hkx_root is not None
    results = []
    for index, hkx_path in enumerate(hkx_paths, start=1):
        try:
            animation = read_animation_hkx_entry(FileBinderEntry(hkx_path), compendium)
            container = animation.animation_container
            try:
                reference_frame_sample_count = len(container.get_reference_frame_samples())
            except (TypeError, ValueError):
                reference_frame_sample_count = 0
            result = {
                "animationId": hkx_path.stem,
                "status": "PASS",
                "animationType": type(container.hkx_animation).__name__,
                "bindingType": type(container.hkx_binding).__name__,
                "blendHint": int(container.hkx_binding.blendHint),
                "originalSkeletonName": container.hkx_binding.originalSkeletonName,
                "frameCount": int(container.hkx_animation.numFrames),
                "durationSeconds": float(container.hkx_animation.duration),
                "transformTrackCount": len(container.get_track_bone_indices()),
                "referenceFrameSampleCount": reference_frame_sample_count,
            }
            if not compact:
                result.update(
                    {
                        "binding": public_values(container.hkx_binding),
                        "animation": public_values(container.hkx_animation),
                        "trackBoneIndices": list(container.get_track_bone_indices()),
                    }
                )
            results.append(result)
        except Exception as exception:
            results.append(
                {
                    "animationId": hkx_path.stem,
                    "status": "FAIL",
                    "error": f"{type(exception).__name__}: {exception}",
                }
            )
        if compact and (index % 100 == 0 or index == len(hkx_paths)):
            print(f"HKX_BINDING_SCAN_PROGRESS:{index}/{len(hkx_paths)}")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(results, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    failed = sum(result["status"] == "FAIL" for result in results)
    print(f"HKX_BINDING_INSPECTION_OK:{len(results)};failed={failed}")
    return 0 if failed == 0 else 2


if __name__ == "__main__":
    raise SystemExit(main())
