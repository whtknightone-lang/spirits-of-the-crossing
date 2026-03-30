"""
Spirits of the Crossing — Myth State System
=============================================
Each planet holds a MythState that tracks which myths are building, active, and at
what narrative tier. Myths activate when mean agent sync on that planet exceeds a
threshold, and decay when sync drops away.

Tiers (from myth_stories.json):
  seedling  — first awakening, gentle presence
  explorer  — deepening contact, pattern recognition
  voyager   — full activation, ancient intelligence stirs

Myth thresholds derived from myth_thresholds.json.
"""

from typing import Dict, Set, Optional


# -----------------------------------------------------------------------
# Myth threshold table (from myth_thresholds.json)
# -----------------------------------------------------------------------

MYTH_THRESHOLDS: Dict[str, dict] = {
    "forest":      {"threshold": 0.34, "boost": 0.25, "decay": 0.04},
    "sky":         {"threshold": 0.18, "boost": 0.15, "decay": 0.05},
    "ocean":       {"threshold": 0.30, "boost": 0.23, "decay": 0.04},
    "source":      {"threshold": 0.17, "boost": 0.14, "decay": 0.03},
    "elder":       {"threshold": 0.23, "boost": 0.18, "decay": 0.02},
    "fire":        {"threshold": 0.10, "boost": 0.04, "decay": 0.06},
    "storm":       {"threshold": 0.10, "boost": 0.02, "decay": 0.07},
    "machine":     {"threshold": 0.14, "boost": 0.12, "decay": 0.04},
    "ruin":        {"threshold": 0.10, "boost": 0.05, "decay": 0.03},
    # meta-myths (no direct threshold — triggered programmatically)
    "rebirth":     {"threshold": 0.50, "boost": 0.40, "decay": 0.01},
    "harmony":     {"threshold": 0.45, "boost": 0.35, "decay": 0.02},
    "convergence": {"threshold": 0.40, "boost": 0.30, "decay": 0.02},
    # general myths (low threshold, social-driven)
    "garden":      {"threshold": 0.25, "boost": 0.20, "decay": 0.04},
    "starlight":   {"threshold": 0.20, "boost": 0.15, "decay": 0.05},
    "wonder":      {"threshold": 0.15, "boost": 0.10, "decay": 0.05},
    "exploration": {"threshold": 0.18, "boost": 0.14, "decay": 0.05},
    "friendship":  {"threshold": 0.22, "boost": 0.17, "decay": 0.04},
    "wanderer":    {"threshold": 0.28, "boost": 0.22, "decay": 0.03},
    "discovery":   {"threshold": 0.20, "boost": 0.15, "decay": 0.04},
    "insight":     {"threshold": 0.25, "boost": 0.19, "decay": 0.03},
}


def _tier(score: float, threshold: float, boost: float) -> Optional[str]:
    """Return narrative tier given a myth score and its threshold values."""
    if score >= boost + threshold:
        return "voyager"
    if score >= threshold:
        return "explorer"
    if score >= threshold * 0.6:
        return "seedling"
    return None


# -----------------------------------------------------------------------
# MythState — lives on each Planet
# -----------------------------------------------------------------------

class MythState:
    """
    Tracks myth activation for one planet.

    Each tick, call update(mean_sync, myth_keys) where:
      mean_sync  — mean Kuramoto sync of agents on this planet
      myth_keys  — which myths are eligible for this planet

    Reads .active   — set of myth_key strings currently at seedling or above
    Reads .tiers    — dict myth_key -> tier string
    Reads .scores   — dict myth_key -> float score
    """

    def __init__(self, myth_keys):
        self.myth_keys = list(myth_keys)
        self.scores: Dict[str, float] = {k: 0.0 for k in myth_keys}
        self.ages:   Dict[str, int]   = {k: 0   for k in myth_keys}
        self.tiers:  Dict[str, Optional[str]] = {k: None for k in myth_keys}
        self.active: Set[str] = set()

    def update(self, mean_sync: float, extra_signal: float = 0.0) -> None:
        """
        Drive each myth score toward mean_sync, then decay.
        extra_signal can be used to boost specific myths externally
        (e.g. Mayan cycle rebirth event).
        """
        self.active.clear()
        for key in self.myth_keys:
            cfg = MYTH_THRESHOLDS.get(key, {"threshold": 0.20, "boost": 0.15, "decay": 0.04})
            # score drifts toward mean_sync + extra
            target = min(1.0, mean_sync + extra_signal)
            self.scores[key] += 0.05 * (target - self.scores[key])
            # decay
            self.scores[key] = max(0.0, self.scores[key] - cfg["decay"] * 0.01)

            tier = _tier(self.scores[key], cfg["threshold"], cfg["boost"])
            self.tiers[key] = tier
            if tier is not None:
                self.active.add(key)
                self.ages[key] += 1

    def force_activate(self, myth_key: str, score: float = 0.8) -> None:
        """
        Force-activate a myth (e.g. rebirth on Mayan cycle turn).
        The score will decay naturally afterward.
        """
        if myth_key not in self.scores:
            self.myth_keys.append(myth_key)
            self.scores[myth_key] = 0.0
            self.ages[myth_key] = 0
            self.tiers[myth_key] = None
        self.scores[myth_key] = max(self.scores.get(myth_key, 0.0), score)

    def highest_tier(self) -> Optional[str]:
        """Return the most advanced tier currently active across all myths."""
        if "voyager" in self.tiers.values():
            return "voyager"
        if "explorer" in self.tiers.values():
            return "explorer"
        if "seedling" in self.tiers.values():
            return "seedling"
        return None

    def summary(self) -> str:
        """Human-readable one-liner for terminal output."""
        active_list = sorted(self.active)
        if not active_list:
            return "quiet"
        parts = [f"{k}({self.tiers[k][0]})" for k in active_list]
        return " ".join(parts)
