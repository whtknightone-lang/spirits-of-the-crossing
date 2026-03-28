"""
Spirit Profile Generator
========================
Runs the RBE-1 simulation under archetype-specific and planet-specific
conditions, samples the equilibrium drive distributions, and exports
a spirit_profiles.json ready for Unity's StreamingAssets.

Output schema
-------------
{
  "spirits": [
    {
      "archetypeId": "Seated",
      "sculptureMode": "SeatedMeditation",
      "driveWeights": { "attack":0.05, "flee":0.08, ... },
      "dominantDrive": "rest",
      "coherenceBaseline": 0.82,
      "spectralSignature": { "red":0.12, "green":0.75, ... }
    }, ...
  ],
  "planets": [
    {
      "planetId": "ForestHeart",
      "worldBias": { "food":0.75, "hazard":0.15, ... },
      "preferredArchetypes": ["Seated", "FlowDancer"]
    }, ...
  ]
}
"""

from __future__ import annotations

import json
import math
import pathlib
import numpy as np

from rbe_1_runnable_prototype import (
    RBE1Prototype, WorldConfig, AgentConfig,
    BrainState, Agent, BANDS, B, BI
)

# ---------------------------------------------------------------------------
# Archetype spectral seed signatures
# Each entry biases the initial brain amplitude toward an archetype's
# characteristic expression. Values are [red, orange, yellow, green,
# blue, indigo, violet].
# ---------------------------------------------------------------------------

ARCHETYPE_SEEDS: dict[str, dict] = {
    "Seated": {
        "sculptureMode": "SeatedMeditation",
        "description": "Still, grounded, high rest drive. Crown and heart dominant.",
        "amplitude_seed": [0.08, 0.15, 0.20, 0.85, 0.12, 0.30, 0.18],
        "world_overrides": {"food_regen": 0.006, "hazard_strength": 0.10,
                            "signal_diffusion": 0.12, "signal_decay": 0.04},
    },
    "FlowDancer": {
        "sculptureMode": "FlowDance",
        "description": "Fluid movement, heart-blue axis, moderate social.",
        "amplitude_seed": [0.12, 0.25, 0.55, 0.60, 0.70, 0.35, 0.30],
        "world_overrides": {"food_regen": 0.004, "hazard_strength": 0.20,
                            "signal_diffusion": 0.22, "signal_decay": 0.05},
    },
    "Dervish": {
        "sculptureMode": "Dervish",
        "description": "High spin/explore, violet-blue axis, low rest.",
        "amplitude_seed": [0.18, 0.20, 0.38, 0.28, 0.75, 0.25, 0.80],
        "world_overrides": {"food_regen": 0.002, "hazard_strength": 0.30,
                            "signal_diffusion": 0.30, "signal_decay": 0.06},
    },
    "PairA": {
        "sculptureMode": "CoupledDance",
        "description": "Social resonance, indigo-orange axis, paired synchrony.",
        "amplitude_seed": [0.15, 0.65, 0.35, 0.50, 0.30, 0.80, 0.28],
        "world_overrides": {"food_regen": 0.004, "hazard_strength": 0.18,
                            "signal_diffusion": 0.28, "signal_decay": 0.04},
    },
    "PairB": {
        "sculptureMode": "CoupledDance",
        "description": "Responsive pair spirit, slightly higher fear/flee, strong social.",
        "amplitude_seed": [0.22, 0.58, 0.30, 0.48, 0.35, 0.75, 0.32],
        "world_overrides": {"food_regen": 0.004, "hazard_strength": 0.22,
                            "signal_diffusion": 0.28, "signal_decay": 0.04},
    },
    # ---- Elder Dragon Spirits ----
    "EarthDragon": {
        "sculptureMode": "DragonManifest",
        "description": "Ancient earth guardian. Deepest rest energy. Grounded, slow, ancient memory.",
        "amplitude_seed": [0.05, 0.60, 0.45, 0.90, 0.10, 0.35, 0.12],
        "world_overrides": {"food_regen": 0.008, "hazard_strength": 0.06,
                            "signal_diffusion": 0.10, "signal_decay": 0.03},
    },
    "FireDragon": {
        "sculptureMode": "DragonManifest",
        "description": "Intense fire spirit. High aggression and chaos. Transforms through challenge.",
        "amplitude_seed": [0.80, 0.35, 0.50, 0.12, 0.72, 0.22, 0.78],
        "world_overrides": {"food_regen": 0.001, "hazard_strength": 0.55,
                            "signal_diffusion": 0.20, "signal_decay": 0.08},
    },
    "WaterDragon": {
        "sculptureMode": "DragonManifest",
        "description": "Fluid water spirit. Social resonance and depth. Flows between worlds.",
        "amplitude_seed": [0.08, 0.20, 0.30, 0.50, 0.85, 0.70, 0.25],
        "world_overrides": {"food_regen": 0.005, "hazard_strength": 0.14,
                            "signal_diffusion": 0.45, "signal_decay": 0.04},
    },
    "ElderAirDragon": {
        "sculptureMode": "DragonManifest",
        "description": "Elder of the sky. Balanced all-band resonance. Seeks and explores endlessly.",
        "amplitude_seed": [0.25, 0.30, 0.40, 0.65, 0.75, 0.45, 0.70],
        "world_overrides": {"food_regen": 0.003, "hazard_strength": 0.15,
                            "signal_diffusion": 0.35, "signal_decay": 0.05},
    },
}

# ---------------------------------------------------------------------------
# Planet world configurations
# ---------------------------------------------------------------------------

PLANET_CONFIGS: dict[str, dict] = {
    "ForestHeart": {
        "description": "Lush, calm, social. High food, low hazard. Earth Dragon realm.",
        "world_overrides": {"food_regen": 0.006, "hazard_strength": 0.10,
                            "signal_diffusion": 0.20, "energy_regen": 0.004},
        "preferredArchetypes": ["Seated", "FlowDancer", "EarthDragon"],
    },
    "SkySpiral": {
        "description": "Aerial, exploratory, spinning. Low hazard, high energy. Air Dragon realm.",
        "world_overrides": {"food_regen": 0.003, "hazard_strength": 0.12,
                            "signal_diffusion": 0.35, "energy_regen": 0.006},
        "preferredArchetypes": ["Dervish", "FlowDancer", "ElderAirDragon"],
    },
    "SourceVeil": {
        "description": "Mystical stillness. High signal patch, low hazard.",
        "world_overrides": {"food_regen": 0.004, "hazard_strength": 0.08,
                            "signal_patch_gain": 0.35, "energy_regen": 0.003},
        "preferredArchetypes": ["Seated", "PairA", "EarthDragon"],
    },
    "WaterFlow": {
        "description": "Fluid, dynamic. Medium food in patches, flowing signal. Water Dragon realm.",
        "world_overrides": {"food_regen": 0.005, "hazard_strength": 0.18,
                            "signal_diffusion": 0.40, "energy_regen": 0.004},
        "preferredArchetypes": ["FlowDancer", "PairB", "WaterDragon"],
    },
    "MachineOrder": {
        "description": "Ordered, disciplined. High terrain variation, moderate hazard.",
        "world_overrides": {"food_regen": 0.003, "hazard_strength": 0.28,
                            "signal_diffusion": 0.10, "energy_regen": 0.002},
        "preferredArchetypes": ["Dervish", "PairA"],
    },
    "DarkContrast": {
        "description": "High challenge. Elevated hazard, sparse food, strong signals. Fire Dragon realm.",
        "world_overrides": {"food_regen": 0.001, "hazard_strength": 0.55,
                            "signal_diffusion": 0.15, "signal_decay": 0.08},
        "preferredArchetypes": ["Dervish", "PairB", "FireDragon"],
    },
}

# ---------------------------------------------------------------------------
# Simulation helpers
# ---------------------------------------------------------------------------

SPECTRAL_STEPS = 80    # short run to let spectral signature naturally evolve
NUM_AGENTS     = 24
WORLD_SIZE     = 48
SEED           = 42


def build_world_config(overrides: dict) -> WorldConfig:
    cfg = WorldConfig(width=WORLD_SIZE, height=WORLD_SIZE,
                      num_agents=NUM_AGENTS, max_steps=SPECTRAL_STEPS)
    for k, v in overrides.items():
        if hasattr(cfg, k):
            setattr(cfg, k, v)
    return cfg


def seed_agent_brains(sim: RBE1Prototype, amplitude_seed: list[float]) -> None:
    """Override the initial amplitude of all agents to match the archetype seed."""
    base = np.array(amplitude_seed, dtype=np.float32)
    for a in sim.agents:
        noise = np.random.normal(0, 0.03, size=B).astype(np.float32)
        a.brain.amplitude = np.clip(base + noise, 0.0, 1.0).astype(np.float32)


def analytical_drives(amplitude_seed: list[float]) -> dict:
    """
    Compute drive values analytically from the spectral amplitude seed.

    Assumes full health/energy (health_frac=1, energy_frac=1) and
    coherent phases (p_rb = p_rg = 1.0, coherence = 1.0), which gives
    the cleanest expression of each archetype's characteristic tendencies.
    The formula mirrors RBE1Prototype.compute_drives() exactly.
    """
    A = np.array(amplitude_seed, dtype=np.float64)
    # With aligned phases: cos(0) = 1  →  phase modifiers = max positive
    p_rb = 1.0  # cos(phase_red - phase_blue)
    p_rg = 1.0  # cos(phase_red - phase_green)

    aggression = max(0.0, (0.70 * A[BI["red"]]    +
                           0.30 * A[BI["blue"]]   -
                           0.40 * A[BI["green"]])) * (0.5 + 0.5 * p_rb)
    fear       = max(0.0, (0.80 * A[BI["red"]]    -
                           0.20 * A[BI["green"]])) * (0.6 + 0.4 * p_rg)
    seek       = max(0.0,  0.60 * A[BI["yellow"]] +
                           0.30 * A[BI["blue"]]   +
                           0.20 * A[BI["violet"]])
    rest       = max(0.0,  0.70 * A[BI["green"]]  -
                           0.30 * A[BI["red"]]    -
                           0.20 * A[BI["violet"]])
    social     = max(0.0,  0.70 * A[BI["indigo"]] +
                           0.20 * A[BI["orange"]])
    explore    = max(0.0,  0.80 * A[BI["violet"]] +
                           0.30 * A[BI["blue"]]   -
                           0.20 * A[BI["green"]])

    raw = {"attack": aggression, "flee": fear,
           "seek": seek, "rest": rest,
           "signal": social, "explore": explore}
    total = sum(raw.values()) or 1.0
    return {k: round(v / total, 4) for k, v in raw.items()}


def spectral_after_short_run(sim: RBE1Prototype) -> tuple[dict, float]:
    """Run a short sim to let the seeded brain evolve naturally, then sample."""
    sim.run(SPECTRAL_STEPS)
    amp_accum = np.zeros(B, dtype=np.float64)
    coherence_sum = 0.0
    count = 0
    for a in sim.agents:
        if a.alive:
            amp_accum += a.brain.amplitude.astype(np.float64)
            coherence_sum += a.brain.coherence
            count += 1
    if count == 0:
        count = 1
    avg_amp = amp_accum / count
    spectral = {band: round(float(avg_amp[i]), 4) for i, band in enumerate(BANDS)}
    coherence = round(coherence_sum / count, 4)
    return spectral, coherence


def run_and_sample(sim: RBE1Prototype, amplitude_seed: list[float]) -> dict:
    """
    Compute drives analytically from the archetype seed (gives clean
    archetype-distinct values), then run a short sim to capture how
    the spectral signature naturally evolves from that starting point.
    """
    drives     = analytical_drives(amplitude_seed)
    spectral, coherence = spectral_after_short_run(sim)
    dominant   = max(drives, key=drives.get)

    return {
        "driveWeights": drives,
        "dominantDrive": dominant,
        "coherenceBaseline": coherence,
        "spectralSignature": spectral,
    }


# ---------------------------------------------------------------------------
# Planet world bias summary (normalised 0–1 values for Unity)
# ---------------------------------------------------------------------------

def planet_world_bias(overrides: dict) -> dict:
    defaults = {"food_regen": 0.003, "hazard_strength": 0.4,
                "signal_diffusion": 0.20, "energy_regen": 0.002,
                "signal_patch_gain": 0.15, "signal_decay": 0.05}
    merged = {**defaults, **overrides}
    return {
        "foodRegen":      round(merged["food_regen"]       / 0.010, 3),
        "hazard":         round(merged["hazard_strength"]  / 0.600, 3),
        "signalFlow":     round(merged["signal_diffusion"] / 0.500, 3),
        "energyRegen":    round(merged["energy_regen"]     / 0.008, 3),
        "signalPatch":    round(merged.get("signal_patch_gain", 0.15) / 0.40, 3),
    }


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def generate_spirit_profiles() -> dict:
    np.random.seed(SEED)

    spirits = []
    for archetype_id, cfg in ARCHETYPE_SEEDS.items():
        print(f"  Simulating archetype: {archetype_id}...")
        wcfg = build_world_config(cfg["world_overrides"])
        sim  = RBE1Prototype(wcfg, AgentConfig(), seed=SEED)
        seed_agent_brains(sim, cfg["amplitude_seed"])
        result = run_and_sample(sim, cfg["amplitude_seed"])

        spirits.append({
            "archetypeId":       archetype_id,
            "sculptureMode":     cfg["sculptureMode"],
            "description":       cfg["description"],
            "driveWeights":      result["driveWeights"],
            "dominantDrive":     result["dominantDrive"],
            "coherenceBaseline": result["coherenceBaseline"],
            "spectralSignature": result["spectralSignature"],
        })

    planets = []
    for planet_id, cfg in PLANET_CONFIGS.items():
        planets.append({
            "planetId":           planet_id,
            "description":        cfg["description"],
            "worldBias":          planet_world_bias(cfg["world_overrides"]),
            "preferredArchetypes": cfg["preferredArchetypes"],
        })

    return {"spirits": spirits, "planets": planets}


def main():
    print("Running RBE-1 spirit profile generator...")
    profiles = generate_spirit_profiles()

    # Write to StreamingAssets location (Unity runtime-readable)
    out_dir = pathlib.Path(__file__).parent / "SpiritsCrossing_Core" / "SpiritAI"
    out_dir.mkdir(parents=True, exist_ok=True)
    out_path = out_dir / "spirit_profiles.json"
    out_path.write_text(json.dumps(profiles, indent=2))
    print(f"\nProfiles written to: {out_path}")

    # Print summary table
    print("\n--- Spirit Drive Summary ---")
    header = f"{'Archetype':<14} {'Dominant':<10} {'Coherence':<11}" + \
             "".join(f" {k[:6]:>7}" for k in ("attack","flee","seek","rest","signal","explore"))
    print(header)
    print("-" * len(header))
    for s in profiles["spirits"]:
        dw = s["driveWeights"]
        row = f"{s['archetypeId']:<14} {s['dominantDrive']:<10} {s['coherenceBaseline']:<11.4f}"
        row += "".join(f" {dw.get(k, 0):>7.4f}" for k in ("attack","flee","seek","rest","signal","explore"))
        print(row)

    print("\n--- Planet World Bias ---")
    for p in profiles["planets"]:
        wb = p["worldBias"]
        print(f"  {p['planetId']:<16}  food={wb['foodRegen']:.2f}  hazard={wb['hazard']:.2f}  "
              f"signal={wb['signalFlow']:.2f}  preferred={p['preferredArchetypes']}")


if __name__ == "__main__":
    main()
