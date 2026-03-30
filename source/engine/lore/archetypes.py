"""
Spirits of the Crossing — Spirit Archetypes
============================================
Nine spirit profiles from spirit_profiles.json.
Each archetype carries drive weights that bias the agent's behaviour:

  seek    → scales IDENTITY_GAIN (reaching outward)
  rest    → raises COHERENCE_FLOOR (stability / groundedness)
  signal  → boosts social match influence (resonance with others)
  explore → scales reinforcement gain (curiosity / upsilon sensitivity)
  attack  → raises COHERENCE_PENALTY tolerance (intensity / challenge)
  flee    → lowers COHERENCE_FLOOR slightly (flight / sensitivity)

Nahuales (Mayan animal spirit guides) correspondence:
  Seated       — Ajaw (sun lord, stillness)
  FlowDancer   — Ik (wind, flowing movement)
  Dervish      — Cauac (storm, spinning transformation)
  PairA        — Ix (jaguar, social/paired signal)
  PairB        — Lamat (rabbit, responsive/social)
  EarthDragon  — Imix (earth crocodile, ancient grounded memory)
  FireDragon   — Chicchan (serpent, intense transformation)
  WaterDragon  — Muluc (moon/water, fluid social depth)
  ElderAirDragon — Chuen (monkey, exploratory wisdom)
"""

from dataclasses import dataclass
from typing import Dict, Optional


@dataclass
class Archetype:
    archetype_id: str
    description: str
    nahual: str                        # Mayan day-sign correspondence
    drive_weights: Dict[str, float]    # seek, rest, signal, explore, attack, flee
    coherence_baseline: float


ARCHETYPES: Dict[str, Archetype] = {
    "Seated": Archetype(
        archetype_id="Seated",
        description="Still, grounded, high rest drive. Crown and heart dominant.",
        nahual="Ajaw",
        drive_weights={
            "attack": 0.00, "flee": 0.00,
            "seek":   0.20, "rest": 0.55,
            "signal": 0.25, "explore": 0.01,
        },
        coherence_baseline=0.9158,
    ),
    "FlowDancer": Archetype(
        archetype_id="FlowDancer",
        description="Fluid movement, heart-blue axis, moderate social.",
        nahual="Ik",
        drive_weights={
            "attack": 0.03, "flee": 0.00,
            "seek":   0.37, "rest": 0.20,
            "signal": 0.18, "explore": 0.21,
        },
        coherence_baseline=0.9158,
    ),
    "Dervish": Archetype(
        archetype_id="Dervish",
        description="High spin/explore, violet-blue axis, low rest.",
        nahual="Cauac",
        drive_weights={
            "attack": 0.12, "flee": 0.04,
            "seek":   0.31, "rest": 0.00,
            "signal": 0.11, "explore": 0.41,
        },
        coherence_baseline=0.9158,
    ),
    "PairA": Archetype(
        archetype_id="PairA",
        description="Social resonance, indigo-orange axis, paired synchrony.",
        nahual="Ix",
        drive_weights={
            "attack": 0.00, "flee": 0.01,
            "seek":   0.23, "rest": 0.16,
            "signal": 0.45, "explore": 0.14,
        },
        coherence_baseline=0.9158,
    ),
    "PairB": Archetype(
        archetype_id="PairB",
        description="Responsive pair spirit, slightly higher fear/flee, strong social.",
        nahual="Lamat",
        drive_weights={
            "attack": 0.04, "flee": 0.05,
            "seek":   0.22, "rest": 0.13,
            "signal": 0.40, "explore": 0.16,
        },
        coherence_baseline=0.9158,
    ),
    "EarthDragon": Archetype(
        archetype_id="EarthDragon",
        description="Ancient earth guardian. Deepest rest energy. Grounded, slow, ancient memory.",
        nahual="Imix",
        drive_weights={
            "attack": 0.00, "flee": 0.00,
            "seek":   0.25, "rest": 0.46,
            "signal": 0.29, "explore": 0.00,
        },
        coherence_baseline=0.9158,
    ),
    "FireDragon": Archetype(
        archetype_id="FireDragon",
        description="Intense fire spirit. High aggression and chaos. Transforms through challenge.",
        nahual="Chicchan",
        drive_weights={
            "attack": 0.24, "flee": 0.20,
            "seek":   0.22, "rest": 0.00,
            "signal": 0.07, "explore": 0.27,
        },
        coherence_baseline=0.9158,
    ),
    "WaterDragon": Archetype(
        archetype_id="WaterDragon",
        description="Fluid water spirit. Social resonance and depth. Flows between worlds.",
        nahual="Muluc",
        drive_weights={
            "attack": 0.06, "flee": 0.00,
            "seek":   0.28, "rest": 0.16,
            "signal": 0.30, "explore": 0.20,
        },
        coherence_baseline=0.9158,
    ),
    "ElderAirDragon": Archetype(
        archetype_id="ElderAirDragon",
        description="Elder of the sky. Balanced all-band resonance. Seeks and explores endlessly.",
        nahual="Chuen",
        drive_weights={
            "attack": 0.07, "flee": 0.03,
            "seek":   0.29, "rest": 0.12,
            "signal": 0.18, "explore": 0.31,
        },
        coherence_baseline=0.9158,
    ),
}


def get_archetype(archetype_id: str) -> Optional[Archetype]:
    return ARCHETYPES.get(archetype_id)


def default_drives() -> Dict[str, float]:
    """Neutral drive weights for agents with no assigned archetype."""
    return {
        "attack": 0.05, "flee": 0.02,
        "seek":   0.25, "rest": 0.20,
        "signal": 0.25, "explore": 0.23,
    }
