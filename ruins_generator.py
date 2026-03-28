"""
Ruins Generator
===============
Derives three layers of world data from the existing cosmos orbital physics:

  ANCIENT RUINS  — vibrational echoes of the first civilizations.
                   Fields amplified toward the planet's orbital equilibrium
                   with violet/source lift (these cultures were deeply connected).

  NEWER RUINS    — partially developed civilizations that didn't complete a cycle.
                   Fields partial, more turbulent, element character preserved.

  ACTIVE WORLDS  — the living planet as it is now. NPC populations, field
                   strength, current growth level.

Each ruin has a discovery threshold: the player's VibrationalField harmony
with the ruin must reach that level to perceive it. Ancient ruins require
more alignment (they vibrate at a deeper, purer frequency).

Output: ruins_data.json
"""

from __future__ import annotations
import json
import math
import pathlib
import random

COSMOS_PATH = pathlib.Path(__file__).parent / "SpiritsCrossing_Core" / "Cosmos" / "cosmos_data.json"
OUT_PATH    = pathlib.Path(__file__).parent / "SpiritsCrossing_Core" / "World"  / "ruins_data.json"

RNG = random.Random(42)

# ---- Ancient era names per element ----
ANCIENT_ERAS = {
    "Fire":   ["The First Flame",    "The Age of Burning Sky"],
    "Earth":  ["The Root Age",       "The Stone Dreaming"],
    "Water":  ["The Deep Tide Era",  "The Time of Still Waters"],
    "Air":    ["The First Breath",   "The Open Sky Civilization"],
    "Source": ["The Silent Age",     "The Before-Time"],
}

NEWER_ERAS = {
    "Fire":   ["The Ember Culture",  "The Interrupted Forge"],
    "Earth":  ["The Forest Keepers", "The Burrowed Cities"],
    "Water":  ["The Reef Builders",  "The Scattered Current"],
    "Air":    ["The Wind Carvers",   "The Half-Woven Sky"],
    "Source": ["The Reaching Age",   "The Unfinished Bridge"],
}

ANCIENT_MYTH = {
    "Fire":   "ruin",   "Earth":  "ruin",
    "Water":  "ruin",   "Air":    "ruin",   "Source": "elder",
}

NEWER_MYTH = {
    "Fire":   "fire",   "Earth":  "forest",
    "Water":  "ocean",  "Air":    "sky",    "Source": "source",
}

BANDS = ["red", "orange", "yellow", "green", "blue", "indigo", "violet"]

def derive_ancient_field(spectral: dict, element: str, variant: int) -> dict:
    """
    Ancient ruins: deep orbital equilibrium, violet-boosted, Source-connected.
    Variant 0 = primary culture (close to equilibrium).
    Variant 1 = secondary culture (different dominant emphasis).
    """
    f = {b: spectral[b] for b in BANDS}

    # Amplify the dominant bands (ancient cultures were more aligned with their orbital field)
    dominant = max(f, key=f.get)
    f[dominant] = min(1.0, f[dominant] + 0.15)

    # Violet and indigo lift — ancient cultures were Source-connected
    f["violet"] = min(1.0, f["violet"] + 0.20)
    f["indigo"] = min(1.0, f["indigo"] + 0.15)

    # Red reduction for non-fire elements (less conflict in ancient eras)
    if element != "Fire":
        f["red"] = max(0.0, f["red"] - 0.15)

    # Variant 1: shift emphasis to secondary band
    if variant == 1:
        bands_sorted = sorted(f, key=f.get, reverse=True)
        secondary = bands_sorted[1]
        f[secondary] = min(1.0, f[secondary] + 0.12)
        f[dominant]  = max(0.0, f[dominant]  - 0.08)

    # Normalise to [0, 1]
    return {b: round(min(1.0, max(0.0, f[b])), 4) for b in BANDS}


def derive_newer_field(spectral: dict, element: str, variant: int) -> dict:
    """
    Newer ruins: partial development, more turbulent, element character preserved.
    """
    f = {b: spectral[b] for b in BANDS}

    # Scale down — less developed civilization
    scale = 0.70 + RNG.uniform(-0.05, 0.05)
    f = {b: v * scale for b, v in f.items()}

    # Add noise — uneven development
    noise_scale = 0.12
    for b in BANDS:
        f[b] = f[b] + RNG.uniform(-noise_scale, noise_scale)
        f[b] = min(1.0, max(0.0, f[b]))

    # Variant 1: more chaotic (didn't reach equilibrium)
    if variant == 1:
        for b in BANDS:
            f[b] = f[b] + RNG.uniform(-0.08, 0.08)
            f[b] = min(1.0, max(0.0, f[b]))

    return {b: round(f[b], 4) for b in BANDS}


def make_ruins_for_planet(planet: dict) -> dict:
    spectral = planet["equilibriumSpectral"]
    element  = planet["element"]
    pid      = planet["planetId"]

    ancient_eras = ANCIENT_ERAS.get(element, ["The First Age", "The Elder Time"])
    newer_eras   = NEWER_ERAS.get(element,   ["The Recent Culture", "The Unfinished Age"])

    # ---- 2 Ancient Ruins ----
    ancient_ruins = []
    for i in range(2):
        angle = round(RNG.uniform(0, 2 * math.pi), 4)
        af    = derive_ancient_field(spectral, element, i)
        ancient_ruins.append({
            "ruinId":             f"{pid.lower()}_ancient_{i+1:02d}",
            "planetId":           pid,
            "layer":              "Ancient",
            "era":                ancient_eras[i % len(ancient_eras)],
            "frozenField":        af,
            "discoveryThreshold": 0.70,
            "mythTrigger":        ANCIENT_MYTH.get(element, "ruin"),
            "orbitalAngleOffset": angle,
            "description":        f"Echoes of {ancient_eras[i % len(ancient_eras)]} on {pid}. "
                                   f"Dominant: {max(af, key=af.get)}.",
        })

    # ---- 2 Newer Ruins ----
    newer_ruins = []
    for i in range(2):
        angle = round(RNG.uniform(0, 2 * math.pi), 4)
        nf    = derive_newer_field(spectral, element, i)
        newer_ruins.append({
            "ruinId":             f"{pid.lower()}_newer_{i+1:02d}",
            "planetId":           pid,
            "layer":              "Newer",
            "era":                newer_eras[i % len(newer_eras)],
            "frozenField":        nf,
            "discoveryThreshold": 0.50,
            "mythTrigger":        NEWER_MYTH.get(element, "ruin"),
            "orbitalAngleOffset": angle,
            "description":        f"Remnants of {newer_eras[i % len(newer_eras)]} on {pid}. "
                                   f"Incomplete transition.",
        })

    # ---- Active World ----
    active_world = {
        "worldId":       f"{pid.lower()}_active",
        "planetId":      pid,
        "layer":         "Active",
        "element":       element,
        "fieldStrength": planet["fieldStrength"],
        "couplingConstant": planet["couplingConstant"],
        "npcArchetype":  planet["npcPopulation"]["naturalArchetype"],
        "npcDrives":     planet["npcPopulation"]["driveDistribution"],
        "ambientField":  spectral,   # the live orbital equilibrium
        "growthRate":    planet["growthRate"],
        "description":   f"Living world of {pid}. {planet['description']}",
        "discoveryThreshold": 0.0,   # always visible
    }

    return {
        "planetId":    pid,
        "element":     element,
        "ancientRuins": ancient_ruins,
        "newerRuins":   newer_ruins,
        "activeWorld":  active_world,
    }


def main():
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    cosmos = json.loads(COSMOS_PATH.read_text())

    all_planets = []
    total_ancient = 0
    total_newer   = 0

    print("Ruins Generator")
    print("=" * 60)
    print(f"{'Planet':<16} {'Element':<8} {'Ancient':>8} {'Newer':>7} {'Active':>7}")
    print("-" * 60)

    for planet in cosmos["planets"]:
        pdata = make_ruins_for_planet(planet)
        all_planets.append(pdata)
        na = len(pdata["ancientRuins"])
        nn = len(pdata["newerRuins"])
        total_ancient += na
        total_newer   += nn
        print(f"  {planet['planetId']:<16} {planet['element']:<8} {na:>8} {nn:>7} {'1':>7}")

    ruins_data = {
        "planets": all_planets,
        "totalAncientRuins": total_ancient,
        "totalNewerRuins":   total_newer,
        "totalActiveWorlds": len(all_planets),
        "generatedAt": __import__("datetime").datetime.utcnow().isoformat() + "Z",
    }

    OUT_PATH.write_text(json.dumps(ruins_data, indent=2))
    print(f"\nTotal: {total_ancient} ancient ruins + {total_newer} newer ruins + {len(all_planets)} active worlds")
    print(f"Written to: {OUT_PATH}")


if __name__ == "__main__":
    main()
