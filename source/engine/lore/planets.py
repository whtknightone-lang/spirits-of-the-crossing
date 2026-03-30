"""
Spirits of the Crossing — Named Planet Lore
============================================
Six worlds derived from the game's spirit_profiles.json and myth_thresholds.json.
Each world has a resonant character, elemental alignment, and preferred spirit archetypes.

Mayan cosmological correspondence:
  ForestHeart  — Ixil (earth / ceiba roots)
  SkySpiral    — Ik (wind / ahau sky)
  SourceVeil   — Xibalba veil (source / underworld entrance)
  WaterFlow    — Chaac (rain / ocean flow)
  MachineOrder — Itzamna order (machine / celestial mechanics)
  DarkContrast — Ah Puch (fire / dark transformation)
"""

from dataclasses import dataclass, field
from typing import Dict, List


@dataclass
class PlanetLore:
    world_id: str
    element: str
    description: str
    world_bias: Dict[str, float]          # foodRegen, hazard, signalFlow, energyRegen, signalPatch
    preferred_archetypes: List[str]
    upsilon_frequency: float              # base Upsilon node oscillation frequency
    myth_keys: List[str]                  # which myths can activate here


# -----------------------------------------------------------------------
# The six named worlds
# -----------------------------------------------------------------------

PLANET_LORE: List[PlanetLore] = [
    PlanetLore(
        world_id="ForestHeart",
        element="earth",
        description="Lush, calm, social. Ancient root-memory. Earth Dragon realm. "
                    "Ceiba tree axis — the world tree that connects all layers.",
        world_bias={
            "foodRegen":  0.60,
            "hazard":     0.167,
            "signalFlow": 0.40,
            "energyRegen": 0.50,
            "signalPatch": 0.375,
        },
        preferred_archetypes=["Seated", "FlowDancer", "EarthDragon"],
        upsilon_frequency=0.25,
        myth_keys=["forest", "garden", "elder", "ruin"],
    ),
    PlanetLore(
        world_id="SkySpiral",
        element="air",
        description="Aerial, exploratory, spinning. Elder Air Dragon realm. "
                    "Ik wind glyphs — the breath of creation that carries spirit.",
        world_bias={
            "foodRegen":  0.30,
            "hazard":     0.20,
            "signalFlow": 0.70,
            "energyRegen": 0.75,
            "signalPatch": 0.375,
        },
        preferred_archetypes=["Dervish", "FlowDancer", "ElderAirDragon"],
        upsilon_frequency=0.55,
        myth_keys=["sky", "starlight", "wonder", "exploration"],
    ),
    PlanetLore(
        world_id="SourceVeil",
        element="source",
        description="Mystical stillness. High signal, low hazard. The veil between worlds. "
                    "Xibalba threshold — the crossing point into the spirit realm.",
        world_bias={
            "foodRegen":  0.40,
            "hazard":     0.133,
            "signalFlow": 0.40,
            "energyRegen": 0.375,
            "signalPatch": 0.875,
        },
        preferred_archetypes=["Seated", "PairA", "EarthDragon"],
        upsilon_frequency=0.30,
        myth_keys=["source", "elder", "ruin", "rebirth", "convergence"],
    ),
    PlanetLore(
        world_id="WaterFlow",
        element="water",
        description="Fluid, dynamic, social. Water Dragon realm. "
                    "Chaac rain serpent — the flowing force that connects and renews.",
        world_bias={
            "foodRegen":  0.50,
            "hazard":     0.30,
            "signalFlow": 0.80,
            "energyRegen": 0.50,
            "signalPatch": 0.375,
        },
        preferred_archetypes=["FlowDancer", "PairB", "WaterDragon"],
        upsilon_frequency=0.40,
        myth_keys=["ocean", "friendship", "harmony", "wanderer"],
    ),
    PlanetLore(
        world_id="MachineOrder",
        element="machine",
        description="Ordered, disciplined. Itzamna celestial mechanics. "
                    "The precision of the Long Count — pattern, cycle, inevitability.",
        world_bias={
            "foodRegen":  0.30,
            "hazard":     0.467,
            "signalFlow": 0.20,
            "energyRegen": 0.25,
            "signalPatch": 0.375,
        },
        preferred_archetypes=["Dervish", "PairA"],
        upsilon_frequency=0.50,
        myth_keys=["machine", "discovery", "insight"],
    ),
    PlanetLore(
        world_id="DarkContrast",
        element="fire",
        description="High challenge. Sparse food, elevated hazard, strong signal. "
                    "Ah Puch fire realm — transformation through the dark passage.",
        world_bias={
            "foodRegen":  0.10,
            "hazard":     0.917,
            "signalFlow": 0.30,
            "energyRegen": 0.25,
            "signalPatch": 0.375,
        },
        preferred_archetypes=["Dervish", "PairB", "FireDragon"],
        upsilon_frequency=0.65,
        myth_keys=["fire", "storm", "rebirth"],
    ),
]

# quick lookup by world_id
PLANET_LORE_BY_ID: Dict[str, PlanetLore] = {p.world_id: p for p in PLANET_LORE}

# Mayan Long Count cycle length in simulation steps
# The 13 b'ak'tuns of the fourth creation = 5200 tun years
# Scaled to simulation time: 5200 steps per great cycle
MAYAN_CYCLE = 5200
