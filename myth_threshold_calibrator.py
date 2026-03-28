"""
Myth Threshold Calibrator
=========================
Derives myth activation thresholds from the spirit drive profiles in
spirit_profiles.json rather than using hand-tuned constants.

Method
------
For each myth key:
1. Define which spirit archetypes embody it and which player resonance
   dimensions drive it (e.g. "source" → sourceAlignment, Seated + PairA).
2. Compute the expected PlayerResonanceScore() for a player whose dominant
   quality is at full strength (1.0) on the relevant dimensions.
3. Set threshold = 0.62 * max_score  (activates when ~62% of potential matched).
4. Derive a secondaryBoost for reinforcing an already-active myth.

Output: myth_thresholds.json
"""

from __future__ import annotations
import json
import pathlib

# ---------------------------------------------------------------------------
# Load spirit profiles
# ---------------------------------------------------------------------------

PROFILES_PATH = pathlib.Path(__file__).parent / "SpiritsCrossing_Core" / "SpiritAI" / "spirit_profiles.json"
OUT_PATH      = pathlib.Path(__file__).parent / "SpiritsCrossing_Core" / "SpiritAI" / "myth_thresholds.json"

def load_profiles() -> dict:
    return json.loads(PROFILES_PATH.read_text())

# ---------------------------------------------------------------------------
# PlayerResonanceScore formula (mirrors SpiritBrainController.PlayerResonanceScore)
#
# score = dw.rest    * calm
#       + dw.seek    * joy
#       + dw.signal  * socialSync
#       + dw.explore * wonder
#       + dw.flee    * (1 - distortion)
#       + dw.attack  * spin
# ---------------------------------------------------------------------------

def player_resonance_score(dw: dict, player: dict) -> float:
    return (dw["rest"]    * player.get("calm",           0.0) +
            dw["seek"]    * player.get("joy",            0.0) +
            dw["signal"]  * player.get("socialSync",     0.0) +
            dw["explore"] * player.get("wonder",         0.0) +
            dw["flee"]    * (1.0 - player.get("distortion", 0.0)) +
            dw["attack"]  * player.get("spin",           0.0))

# ---------------------------------------------------------------------------
# Myth definitions
# Each entry specifies:
#   archetypes   : which spirits embody this myth
#   player_dims  : player dimensions to set to 1.0 when computing max score
#   planet        : associated planet (for cross-validation)
#   decay_rate   : how fast this myth naturally decays per session
# ---------------------------------------------------------------------------

MYTH_DEFS = {
    "source": {
        "archetypes":   ["Seated", "PairA"],
        "player_dims":  {"sourceAlignment": 1.0, "calm": 0.9},
        "planet":       "SourceVeil",
        "decay_rate":   0.03,
        "description":  "Deep alignment with the source field. Activates through stillness and crown connection.",
    },
    "forest": {
        "archetypes":   ["Seated", "FlowDancer"],
        "player_dims":  {"calm": 1.0, "joy": 0.8, "movementFlow": 0.7},
        "planet":       "ForestHeart",
        "decay_rate":   0.04,
        "description":  "Lush, grounded resonance. Activates through calm flow with low distortion.",
    },
    "sky": {
        "archetypes":   ["Dervish", "FlowDancer"],
        "player_dims":  {"spin": 1.0, "wonder": 0.8, "movementFlow": 0.7},
        "planet":       "SkySpiral",
        "decay_rate":   0.05,
        "description":  "Aerial, exploratory presence. Activates through spinning movement and wonder.",
    },
    "ocean": {
        "archetypes":   ["FlowDancer", "PairB"],
        "player_dims":  {"calm": 0.8, "joy": 0.7, "socialSync": 0.6},
        "planet":       "WaterFlow",
        "decay_rate":   0.04,
        "description":  "Fluid, rhythmic harmony. Activates through flowing movement and social sync.",
    },
    "fire": {
        "archetypes":   ["Dervish", "PairB"],
        "player_dims":  {"spin": 0.9, "distortion": 0.7, "movementFlow": 0.8},
        "planet":       "DarkContrast",
        "decay_rate":   0.06,
        "description":  "Intense contrast and will. Activates through high spin with distortion.",
    },
    "machine": {
        "archetypes":   ["Dervish", "PairA"],
        "player_dims":  {"spin": 1.0, "socialSync": 0.7},
        "planet":       "MachineOrder",
        "decay_rate":   0.04,
        "description":  "Ordered, disciplined resonance. Activates through precise spin and paired synchrony.",
    },
    "storm": {
        "archetypes":   ["Dervish", "PairB"],
        "player_dims":  {"distortion": 1.0, "spin": 0.6},
        "planet":       "DarkContrast",
        "decay_rate":   0.07,
        "description":  "Turbulent contrast field. Activates when distortion is high and unchecked.",
    },
    "elder": {
        "archetypes":   ["Seated", "PairA"],
        "player_dims":  {"calm": 1.0, "wonder": 0.9, "sourceAlignment": 0.9},
        "planet":       "SourceVeil",
        "decay_rate":   0.02,
        "description":  "Ancient presence. Requires sustained stillness, wonder, and source alignment together.",
    },
    "ruin": {
        "archetypes":   ["Seated", "FlowDancer", "PairA"],
        "player_dims":  {"wonder": 1.0, "sourceAlignment": 0.7},
        "planet":       "SourceVeil",
        "decay_rate":   0.03,
        "description":  "Echo of ancient structures. Activates through wonder in combination with source presence.",
    },
}

# Activation factor: threshold = ACTIVATE_FACTOR * max_achievable_score
ACTIVATE_FACTOR = 0.62
# Secondary boost factor: threshold to reinforce an already-active myth
BOOST_FACTOR    = 0.42

# ---------------------------------------------------------------------------
# Calibrate
# ---------------------------------------------------------------------------

def calibrate(profiles: dict) -> dict:
    spirit_index = {s["archetypeId"]: s for s in profiles["spirits"]}

    thresholds = {}

    for myth_key, defn in MYTH_DEFS.items():
        max_scores = []

        for archetype_id in defn["archetypes"]:
            spirit = spirit_index.get(archetype_id)
            if spirit is None:
                continue
            dw = spirit["driveWeights"]
            score = player_resonance_score(dw, defn["player_dims"])
            max_scores.append(score)

        if not max_scores:
            continue

        avg_max   = sum(max_scores) / len(max_scores)
        threshold = round(avg_max * ACTIVATE_FACTOR, 4)
        boost_t   = round(avg_max * BOOST_FACTOR, 4)

        # Coherence contribution: myths with high-coherence spirits need
        # slightly less raw score to activate (the signal is cleaner)
        coherence_avg = sum(
            spirit_index[a]["coherenceBaseline"]
            for a in defn["archetypes"] if a in spirit_index
        ) / max(1, len(defn["archetypes"]))

        # Coherence discount: up to -0.04 on threshold
        coherence_discount = round(0.04 * (coherence_avg - 0.5) * 2.0, 4)
        threshold = round(max(0.10, threshold - coherence_discount), 4)

        thresholds[myth_key] = {
            "threshold":          threshold,
            "boostThreshold":     boost_t,
            "decayRate":          defn["decay_rate"],
            "derivedFrom":        defn["archetypes"],
            "planet":             defn["planet"],
            "avgMaxScore":        round(avg_max, 4),
            "coherenceDiscount":  coherence_discount,
            "description":        defn["description"],
        }

    return thresholds


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    profiles   = load_profiles()
    thresholds = calibrate(profiles)

    OUT_PATH.write_text(json.dumps(thresholds, indent=2))
    print(f"Thresholds written to: {OUT_PATH}\n")

    print(f"{'Myth':<12} {'Threshold':>10} {'Boost':>7} {'AvgMax':>8} {'Derived from'}")
    print("-" * 65)
    for key, t in thresholds.items():
        print(f"{key:<12} {t['threshold']:>10.4f} {t['boostThreshold']:>7.4f} "
              f"{t['avgMaxScore']:>8.4f}  {', '.join(t['derivedFrom'])}")


if __name__ == "__main__":
    main()
