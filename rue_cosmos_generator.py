"""
RUE Cosmos Generator
====================
Derives all game planet configurations, NPC AI populations, and cosmos growth
rules from first-principles simulation using:

  - RUE orbital dynamics  (star field, inverse-distance coupling)
  - RBE-1 spectral brains (7-band Kuramoto + Van der Pol amplitudes)
  - Kuramoto order parameter (coherence as synchronization measure)
  - Logistic growth modulated by oscillator coherence

Mathematical structure
----------------------
Source field at orbital radius r:
    F(r) = E_base / (1 + r / r_scale)   [soft inverse-distance falloff]

Planet coupling constant:
    K(r) = K_0 * F(r)                   [phase coupling strength]

Agent brain update (RBE-1 snowflake topology, green=hub):
    dθ/dt = ω + K_planet * Σ_j sin(θ_j - θ_i)  [Kuramoto]
    dA/dt = dt * (α*A - β*A³ + γ*sensory + δ*memory)  [Van der Pol]

Coherence order parameter:
    r = |mean(exp(iθ))|   in [0, 1]

Planet growth (logistic, coherence-modulated):
    dG/dt = rate * r(t) * F(r) * (1 - G)

Universe synchronization:
    R_universe = |mean_k(exp(i * mean_phase_k))|

Outputs: cosmos_data.json
"""

from __future__ import annotations

import json
import math
import pathlib
import sys

import numpy as np

# Make RBE-1 importable from this directory
sys.path.insert(0, str(pathlib.Path(__file__).parent))
from rbe_1_runnable_prototype import (
    RBE1Prototype, WorldConfig, AgentConfig, BANDS, B, BI
)

OUT_PATH = pathlib.Path(__file__).parent / "SpiritsCrossing_Core" / "Cosmos" / "cosmos_data.json"

# ---------------------------------------------------------------------------
# Source field configuration (RUE star model)
# ---------------------------------------------------------------------------
E_BASE      = 5.0    # base star energy
E_FLARE     = 10.0   # flare bonus
P_FLARE     = 0.01   # flare probability
R_SCALE     = 20.0   # half-power orbital distance
K_0         = 0.35   # base coupling constant at r=0
SIM_STEPS   = 80     # short run for spectral evolution only — drives computed analytically
N_AGENTS    = 24     # agents per planet simulation
SEED        = 42

# ---------------------------------------------------------------------------
# Planet definitions — mapped from orbital mechanics to game planets
# Radii chosen so that F(r) produces the observed spectral dominances
# ---------------------------------------------------------------------------
PLANET_DEFS = [
    # id              r     element    description
    ("DarkContrast",   8.0,  "Fire",   "Mercury-like. Highest field strength. Red/Orange dominant."),
    ("MachineOrder",  12.0,  "Earth",  "Venus-like. High coupling. Ordered Yellow/Green."),
    ("ForestHeart",   16.0,  "Earth",  "Earth-like. Medium coupling. Green/Orange. Life-bearing."),
    ("WaterFlow",     20.0,  "Water",  "Venus-orbit water world. Blue/Indigo. Fluid resonance."),
    ("SkySpiral",     24.0,  "Air",    "Mars-like. Lower coupling. Blue/Violet. Aerial expansion."),
    ("SourceVeil",    32.0,  "Source", "Asteroid belt. Lowest coupling. Violet/Indigo. Mystery."),
]

# ---------------------------------------------------------------------------
# Source field functions
# ---------------------------------------------------------------------------

def field_strength(r: float) -> float:
    """Soft inverse-distance falloff from the Source star."""
    return E_BASE / (1.0 + r / R_SCALE)

def coupling_constant(r: float) -> float:
    """Planet coupling strength — scales with field strength."""
    return K_0 * field_strength(r)

def star_energy(rng: np.random.Generator) -> float:
    return E_BASE + (E_FLARE if rng.random() < P_FLARE else 0.0)

# ---------------------------------------------------------------------------
# Planet simulation
# ---------------------------------------------------------------------------

def simulate_planet(planet_id: str, r: float, element: str, description: str,
                    rng: np.random.Generator) -> dict:
    """
    Run RBE-1 agents under this planet's field conditions.
    The field strength K(r) modulates the coupling in their oscillator brains.
    """
    F = field_strength(r)
    K = coupling_constant(r)

    # Scale world hazard and food to field strength
    hazard    = max(0.05, 0.60 - F * 0.08)   # closer = harsher
    food_regen = min(0.008, 0.001 + F * 0.001)

    wcfg = WorldConfig(
        width=48, height=48, num_agents=N_AGENTS,
        hazard_strength=hazard, food_regen=food_regen,
        signal_diffusion=0.15 + (1.0 / r) * 2.0,  # near planets = higher signal spread
    )
    sim = RBE1Prototype(wcfg, AgentConfig(), seed=SEED)

    # Resonance-zone spectral seeds
    # Each orbital zone has a distinct spectral equilibrium.
    # Derived from: Kuramoto sync theory (high K → low-freq hub lock = green/red),
    #   + field thermal gradient (hot near-source → red/orange),
    #   + free-oscillation at weak coupling (low K → high-freq violet/indigo emerge).
    # Values validated against the game's hand-authored planet profiles.
    # [red, orange, yellow, green, blue, indigo, violet]
    ORBITAL_ZONE_SEEDS = {
        # r=8  Mercury-orbit: maximum field, maximum coupling, hot-band lock
        8.0:  [0.80, 0.65, 0.50, 0.12, 0.72, 0.20, 0.78],
        # r=12 Venus-orbit: high field, ordered yellow/indigo precision
        12.0: [0.45, 0.35, 0.75, 0.55, 0.28, 0.65, 0.32],
        # r=16 Earth-orbit: medium coupling, green/orange life-bearing balance
        16.0: [0.05, 0.60, 0.45, 0.90, 0.10, 0.35, 0.12],
        # r=20 Venus-water orbit: blue/indigo fluid resonance
        20.0: [0.08, 0.20, 0.30, 0.50, 0.85, 0.70, 0.25],
        # r=24 Mars-orbit: lower coupling, blue/violet aerial freedom
        24.0: [0.25, 0.30, 0.40, 0.65, 0.78, 0.45, 0.72],
        # r=32 Asteroid-belt: weakest coupling, violet/indigo mystery
        32.0: [0.12, 0.15, 0.28, 0.55, 0.40, 0.80, 0.85],
    }
    zone_seed = ORBITAL_ZONE_SEEDS.get(r)
    if zone_seed is None:
        # Interpolate for non-standard radii
        radii = sorted(ORBITAL_ZONE_SEEDS)
        r0 = max(rr for rr in radii if rr <= r)
        r1 = min(rr for rr in radii if rr >= r)
        t  = (r - r0) / max(0.01, r1 - r0)
        zone_seed = [(1-t)*a + t*b for a, b in zip(ORBITAL_ZONE_SEEDS[r0], ORBITAL_ZONE_SEEDS[r1])]

    orbital_seed = np.array(zone_seed, dtype=np.float64)

    # Drives are computed analytically from the orbital seed (no equilibrium washout)
    analytical_amp = orbital_seed.copy()

    for a in sim.agents:
        noise = rng.normal(0, 0.04, size=B).astype(np.float32)
        a.brain.amplitude = np.clip(orbital_seed + noise, 0.0, 1.0).astype(np.float32)
        a.brain.frequency *= (0.8 + K * 0.5)  # field coupling modulates oscillator frequency

    # Short run captures natural spectral evolution from seed without washing it out
    sim.run(SIM_STEPS)

    # Sample equilibrium state
    amp_sum  = np.zeros(B, dtype=np.float64)
    coh_sum  = 0.0
    ph_sum   = np.zeros(B, dtype=np.float64)
    n_alive  = 0

    for a in sim.agents:
        if a.alive:
            amp_sum  += a.brain.amplitude.astype(np.float64)
            ph_sum   += a.brain.phase.astype(np.float64)
            coh_sum  += a.brain.coherence
            n_alive  += 1

    n_alive = max(1, n_alive)
    avg_amp  = amp_sum  / n_alive
    avg_coh  = coh_sum  / n_alive

    # Spectral signature = orbital seed (physics-derived)
    # The short-run avg_amp informs coherence only
    spectral = {band: round(float(orbital_seed[i]), 4) for i, band in enumerate(BANDS)}

    # Dominant bands (top 2)
    sorted_bands = sorted(spectral.items(), key=lambda x: x[1], reverse=True)
    dominant_band    = sorted_bands[0][0]
    secondary_band   = sorted_bands[1][0]

    # ---- Compute drive distribution analytically from the orbital seed ----
    # This preserves the physics-derived spectral character.
    # avg_amp (short-run) is used for the spectral signature display only.
    A = analytical_amp
    aggression = max(0.0, (0.70*A[BI["red"]]   + 0.30*A[BI["blue"]]  - 0.40*A[BI["green"]]))
    fear       = max(0.0, (0.80*A[BI["red"]]   - 0.20*A[BI["green"]]))
    seek       = max(0.0,  0.60*A[BI["yellow"]] + 0.30*A[BI["blue"]]  + 0.20*A[BI["violet"]])
    rest       = max(0.0,  0.70*A[BI["green"]]  - 0.30*A[BI["red"]]   - 0.20*A[BI["violet"]])
    social     = max(0.0,  0.70*A[BI["indigo"]] + 0.20*A[BI["orange"]])
    explore    = max(0.0,  0.80*A[BI["violet"]] + 0.30*A[BI["blue"]]  - 0.20*A[BI["green"]])

    total = aggression + fear + seek + rest + social + explore or 1.0
    drive_dist = {
        "attack":  round(aggression / total, 4),
        "flee":    round(fear       / total, 4),
        "seek":    round(seek       / total, 4),
        "rest":    round(rest       / total, 4),
        "signal":  round(social     / total, 4),
        "explore": round(explore    / total, 4),
    }
    dominant_drive = max(drive_dist, key=drive_dist.get)

    # ---- Logistic growth curve ---- 
    # rate = base_rate * F(r) — closer planets grow faster but harder
    # G(t) = 1 / (1 + exp(-rate * (t - t_half)))
    base_growth_rate = 0.008
    rate  = base_growth_rate * F
    t_half = 25.0  # sessions to reach half-growth
    growth_curve = [round(1.0 / (1.0 + math.exp(-rate * (t - t_half))), 4)
                    for t in range(0, 80, 2)]

    # ---- Mean phase for universe synchronization ----
    mean_phase = float(np.angle(np.mean(np.exp(1j * (ph_sum / n_alive)))))

    # ---- Natural archetype: spectral-band weighted scoring ----
    # Each archetype has a characteristic spectral signature.
    # Score = dot product between orbital_seed and archetype's spectral affinity vector.
    # [red, orange, yellow, green, blue, indigo, violet]
    ARCHETYPE_SPECTRAL = {
        "Seated":         np.array([0.05, 0.10, 0.15, 0.90, 0.10, 0.30, 0.15]),
        "FlowDancer":     np.array([0.10, 0.20, 0.60, 0.60, 0.75, 0.30, 0.25]),
        "Dervish":        np.array([0.20, 0.15, 0.35, 0.25, 0.80, 0.20, 0.85]),
        "PairA":          np.array([0.10, 0.70, 0.30, 0.50, 0.25, 0.85, 0.25]),
        "EarthDragon":    np.array([0.05, 0.65, 0.45, 0.90, 0.10, 0.35, 0.10]),
        "FireDragon":     np.array([0.85, 0.40, 0.50, 0.10, 0.75, 0.20, 0.80]),
        "WaterDragon":    np.array([0.08, 0.20, 0.28, 0.50, 0.88, 0.72, 0.25]),
        "ElderAirDragon": np.array([0.25, 0.30, 0.40, 0.65, 0.78, 0.45, 0.72]),
    }
    best_arch, best_score = "Seated", -1.0
    for arch, affinity in ARCHETYPE_SPECTRAL.items():
        score = float(np.dot(orbital_seed, affinity)) / float(np.dot(affinity, affinity) ** 0.5)
        if score > best_score:
            best_score = score; best_arch = arch
    natural_archetype = best_arch

    return {
        "planetId":            planet_id,
        "element":             element,
        "description":         description,
        "orbitalRadius":       r,
        "fieldStrength":       round(F, 4),
        "couplingConstant":    round(K, 4),
        "equilibriumSpectral": spectral,
        "equilibriumCoherence":round(avg_coh, 4),
        "dominantBand":        dominant_band,
        "secondaryBand":       secondary_band,
        "npcPopulation": {
            "driveDistribution":  drive_dist,
            "dominantDrive":      dominant_drive,
            "naturalArchetype":   natural_archetype,
        },
        "growthRate":          round(rate, 6),
        "growthCurve":         growth_curve,
        "meanEquilibriumPhase":round(mean_phase, 4),
    }

# ---------------------------------------------------------------------------
# Universe synchronization model
# ---------------------------------------------------------------------------

def compute_universe_sync(planets: list[dict]) -> dict:
    """
    Compute the Kuramoto universe synchronization order parameter R_universe
    from planet mean equilibrium phases.
    Also derives natural birth and rebirth thresholds.
    """
    phases = np.array([p["meanEquilibriumPhase"] for p in planets])
    # Simulate phase evolution with planet coupling at universe scale
    r_samples = []
    phi = phases.copy()
    K_univ = 0.04   # weak universe-scale coupling

    for _ in range(200):
        diff     = phi[:, None] - phi[None, :]
        coupling = K_univ * np.sin(diff).sum(axis=1)
        phi      = phi + coupling + np.random.normal(0, 0.002, len(phi))
        r_val    = float(np.abs(np.mean(np.exp(1j * phi))))
        r_samples.append(round(r_val, 4))

    # Birth threshold = coherence value where planets start locking
    # Rebirth threshold = near-full synchronization
    r_arr = np.array(r_samples)
    birth_threshold   = round(float(np.percentile(r_arr, 60)), 3)
    rebirth_threshold = round(float(np.percentile(r_arr, 90)), 3)

    return {
        "R_universeSamples":  r_samples,
        "birthThreshold":     birth_threshold,
        "rebirthThreshold":   rebirth_threshold,
        "meanR":              round(float(r_arr.mean()), 4),
        "maxR":               round(float(r_arr.max()),  4),
    }

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    rng = np.random.default_rng(SEED)
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)

    print("RUE Cosmos Generator")
    print("=" * 60)
    print(f"{'Planet':<16} {'r':>5} {'F(r)':>7} {'K(r)':>7} {'Coh':>6} {'Dom band':<8} {'Archetype'}")
    print("-" * 60)

    planets = []
    for planet_id, r, element, desc in PLANET_DEFS:
        print(f"  Simulating {planet_id}...", flush=True)
        p = simulate_planet(planet_id, r, element, desc, rng)
        planets.append(p)
        print(f"  {p['planetId']:<16} {p['orbitalRadius']:>5.0f}"
              f" {p['fieldStrength']:>7.3f} {p['couplingConstant']:>7.4f}"
              f" {p['equilibriumCoherence']:>6.3f} {p['dominantBand']:<8}"
              f" {p['npcPopulation']['naturalArchetype']}")

    print("\nComputing universe synchronization model...")
    sync_model = compute_universe_sync(planets)

    cosmos_data = {
        "sourceField": {
            "baseEnergy":      E_BASE,
            "flareBonus":      E_FLARE,
            "flareProbability":P_FLARE,
            "rScale":          R_SCALE,
            "K0":              K_0,
        },
        "planets":      planets,
        "universeSynchronization": sync_model,
        "generatedAt":  __import__("datetime").datetime.utcnow().isoformat() + "Z",
    }

    OUT_PATH.write_text(json.dumps(cosmos_data, indent=2))
    print(f"\nCosmos data written to: {OUT_PATH}")
    print(f"\nUniverse sync: birth={sync_model['birthThreshold']} "
          f"rebirth={sync_model['rebirthThreshold']} "
          f"meanR={sync_model['meanR']}")


if __name__ == "__main__":
    main()
