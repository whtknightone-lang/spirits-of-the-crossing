"""
Setup Streaming Assets
======================
Automates Step 1: copies all runtime JSON files into Unity's StreamingAssets
folder so they can be loaded at runtime by the game's loader singletons.

Usage
-----
  python setup_streaming_assets.py                        # auto-detects Unity project
  python setup_streaming_assets.py --unity path/to/Unity # explicit project path

Files copied
------------
  SpiritsCrossing_Core/SpiritAI/spirit_profiles.json
  SpiritsCrossing_Core/SpiritAI/myth_thresholds.json
  SpiritsCrossing_Core/BiometricInput/biometric_profiles.json
  SpiritsCrossing_Core/Companions/companion_registry.json

These files are loaded by:
  SpiritProfileLoader.cs         → spirit_profiles.json
  MythInterpreter.cs             → myth_thresholds.json
  SimulatedPhysicalInputReader.cs → biometric_profiles.json
  CompanionBondSystem.cs         → companion_registry.json
"""

from __future__ import annotations
import argparse
import pathlib
import shutil
import sys

ROOT = pathlib.Path(__file__).parent

# Source → destination filename mapping
JSON_FILES = {
    ROOT / "SpiritsCrossing_Core" / "SpiritAI"       / "spirit_profiles.json":    "spirit_profiles.json",
    ROOT / "SpiritsCrossing_Core" / "SpiritAI"       / "myth_thresholds.json":    "myth_thresholds.json",
    ROOT / "SpiritsCrossing_Core" / "BiometricInput" / "biometric_profiles.json": "biometric_profiles.json",
    ROOT / "SpiritsCrossing_Core" / "Companions"     / "companion_registry.json": "companion_registry.json",
    ROOT / "SpiritsCrossing_Core" / "Cosmos"         / "cosmos_data.json":        "cosmos_data.json",
    ROOT / "SpiritsCrossing_Core" / "World"       / "ruins_data.json":         "ruins_data.json",
    ROOT / "SpiritsCrossing_Core" / "ForestWorld" / "forest_world_data.json":  "forest_world_data.json",
}

# Common Unity project subfolder names to search for
UNITY_MARKERS = ["Assets", "ProjectSettings", "Packages"]


def find_unity_project(start: pathlib.Path) -> pathlib.Path | None:
    """Walk up from start looking for a directory that looks like a Unity project."""
    for candidate in [start, *start.parents]:
        if all((candidate / m).exists() for m in UNITY_MARKERS):
            return candidate
    # Also search sibling directories of start
    for sibling in start.parent.iterdir():
        if sibling.is_dir() and all((sibling / m).exists() for m in UNITY_MARKERS):
            return sibling
    return None


def streaming_assets_path(unity_root: pathlib.Path) -> pathlib.Path:
    return unity_root / "Assets" / "StreamingAssets"


def copy_files(unity_root: pathlib.Path, dry_run: bool = False) -> list[tuple[str, str]]:
    sa = streaming_assets_path(unity_root)
    if not dry_run:
        sa.mkdir(parents=True, exist_ok=True)

    results = []
    for src, dst_name in JSON_FILES.items():
        dst = sa / dst_name
        if not src.exists():
            results.append(("MISSING", str(src)))
            print(f"  [MISSING] Source not found: {src}")
            continue
        if not dry_run:
            shutil.copy2(src, dst)
        status = "DRY-RUN" if dry_run else "COPIED"
        results.append((status, str(dst)))
        print(f"  [{status}] {src.name} → {dst}")

    return results


def main():
    parser = argparse.ArgumentParser(description="Copy JSON files to Unity StreamingAssets.")
    parser.add_argument("--unity", type=str, default=None,
                        help="Path to the Unity project root (auto-detected if omitted).")
    parser.add_argument("--dry-run", action="store_true",
                        help="Print what would be copied without actually copying.")
    args = parser.parse_args()

    if args.unity:
        unity_root = pathlib.Path(args.unity).expanduser().resolve()
        if not unity_root.exists():
            print(f"Error: Unity project path does not exist: {unity_root}")
            sys.exit(1)
    else:
        unity_root = find_unity_project(ROOT)
        if unity_root is None:
            print("Could not auto-detect a Unity project. Use --unity path/to/project.")
            print(f"\nLooking for a directory containing: {', '.join(UNITY_MARKERS)}")
            print("Files that would be copied:")
            for src, name in JSON_FILES.items():
                print(f"  {src.name}")
            sys.exit(0)

    sa = streaming_assets_path(unity_root)
    print(f"\nUnity project:    {unity_root}")
    print(f"StreamingAssets:  {sa}")
    print(f"Mode:             {'DRY RUN' if args.dry_run else 'COPY'}\n")

    results = copy_files(unity_root, dry_run=args.dry_run)

    success = sum(1 for status, _ in results if status in ("COPIED", "DRY-RUN"))
    missing = sum(1 for status, _ in results if status == "MISSING")

    print(f"\n{'─' * 50}")
    print(f"Done. {success} file(s) {'would be ' if args.dry_run else ''}copied. {missing} missing.")
    if missing > 0:
        print("Run the generators first:")
        print("  python spirit_profile_generator.py")
        print("  python myth_threshold_calibrator.py")
        print("  python biometric_simulator.py")
        print("  python companion_registry_generator.py")
        print("  python ruins_generator.py")
        print("  python forest_world_generator.py")

    if success > 0 and not args.dry_run:
        print("\nNext steps in Unity:")
        print("  [2] Add GameBootstrapper to Bootstrap scene root (auto-creates all systems)")
        print("  [3] Add CompanionBehaviorController to each companion prefab, set animalId")
        print("  [+] ResonanceMemorySystem, CompanionBondSystem, CosmosGenerationSystem")
        print("      are all created automatically by VRBootstrapInstaller")


if __name__ == "__main__":
    main()
