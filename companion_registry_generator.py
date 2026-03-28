"""
Companion Registry Generator
=============================
Defines all 28 elemental animal companions and exports companion_registry.json
ready for Unity StreamingAssets.

Each companion has:
  - element + tier (1=small, 2=medium, 3=elder)
  - resonanceWeights: which player dimensions grow the bond
  - bondThreshold: minimum weighted score to unlock
  - behaviorMode: dominant personality
  - mythTrigger: myth key reinforced at full bond
  - npcArchetype: which spirit archetype this companion naturally follows
  - preferredPlanet: the realm they inhabit

Bond score formula (mirrors CompanionBondSystem.cs):
  score = sum(weight[dim] * player[dim]) / sum(weights)
"""

from __future__ import annotations
import json
import pathlib

OUT_PATH = pathlib.Path(__file__).parent / "SpiritsCrossing_Core" / "Companions" / "companion_registry.json"

# resonanceWeights keys match PlayerResonanceState fields
DIMS = ["calm", "joy", "wonder", "socialSync", "movementFlow",
        "spinStability", "sourceAlignment", "breathCoherence"]

def w(**kwargs) -> dict:
    """Build a resonance weight dict, defaulting missing dims to 0."""
    return {d: round(kwargs.get(d, 0.0), 3) for d in DIMS}


COMPANIONS = [

    # =========================================================================
    # AIR — SkySpiral / ElderAirDragon realm
    # =========================================================================
    {
        "animalId":       "raven",
        "displayName":    "Raven",
        "element":        "Air",
        "tier":           1,
        "behaviorMode":   "wise",
        "description":    "Keeper of omens and riddles. Watches from high branches and mirrors the player's deepest curiosity.",
        "resonanceWeights": w(wonder=0.55, sourceAlignment=0.35, breathCoherence=0.10),
        "bondThreshold":  0.32,
        "mythTrigger":    "ruin",
        "preferredPlanet":"SkySpiral",
        "npcArchetype":   "Dervish",
    },
    {
        "animalId":       "macaw",
        "displayName":    "Macaw",
        "element":        "Air",
        "tier":           1,
        "behaviorMode":   "playful",
        "description":    "Bright social spirit. Amplifies joy and draws companions into vocal resonance.",
        "resonanceWeights": w(joy=0.55, socialSync=0.35, movementFlow=0.10),
        "bondThreshold":  0.28,
        "mythTrigger":    "sky",
        "preferredPlanet":"SkySpiral",
        "npcArchetype":   "PairA",
    },
    {
        "animalId":       "hawk",
        "displayName":    "Hawk",
        "element":        "Air",
        "tier":           2,
        "behaviorMode":   "precise",
        "description":    "Master of focus and fast strikes. Awakens through spinning clarity and directed movement.",
        "resonanceWeights": w(spinStability=0.45, movementFlow=0.35, breathCoherence=0.20),
        "bondThreshold":  0.40,
        "mythTrigger":    "sky",
        "preferredPlanet":"SkySpiral",
        "npcArchetype":   "Dervish",
    },
    {
        "animalId":       "eagle",
        "displayName":    "Eagle",
        "element":        "Air",
        "tier":           2,
        "behaviorMode":   "soaring",
        "description":    "Clear vision from altitude. Bonds through wonder and source alignment — the capacity to see far.",
        "resonanceWeights": w(sourceAlignment=0.45, wonder=0.35, breathCoherence=0.20),
        "bondThreshold":  0.44,
        "mythTrigger":    "elder",
        "preferredPlanet":"SkySpiral",
        "npcArchetype":   "ElderAirDragon",
    },
    {
        "animalId":       "harpy_eagle",
        "displayName":    "Harpy Eagle",
        "element":        "Air",
        "tier":           3,
        "behaviorMode":   "fierce-wise",
        "description":    "Apex of air spirits. Combines fierce precision with ancient source sight. The most demanding air bond.",
        "resonanceWeights": w(spinStability=0.35, sourceAlignment=0.40, wonder=0.25),
        "bondThreshold":  0.55,
        "mythTrigger":    "elder",
        "preferredPlanet":"SkySpiral",
        "npcArchetype":   "ElderAirDragon",
    },

    # =========================================================================
    # EARTH — ForestHeart / EarthDragon realm
    # =========================================================================
    {
        "animalId":       "groundhog",
        "displayName":    "Groundhog",
        "element":        "Earth",
        "tier":           1,
        "behaviorMode":   "cautious",
        "description":    "Underground wisdom. Emerges only when the player is genuinely still and breath is coherent.",
        "resonanceWeights": w(calm=0.55, breathCoherence=0.35, sourceAlignment=0.10),
        "bondThreshold":  0.30,
        "mythTrigger":    "forest",
        "preferredPlanet":"ForestHeart",
        "npcArchetype":   "Seated",
    },
    {
        "animalId":       "mouse",
        "displayName":    "Mouse",
        "element":        "Earth",
        "tier":           1,
        "behaviorMode":   "gentle",
        "description":    "Tiny ambassador of community. Responds to calm social presence and gentle breath.",
        "resonanceWeights": w(calm=0.50, socialSync=0.35, breathCoherence=0.15),
        "bondThreshold":  0.27,
        "mythTrigger":    "forest",
        "preferredPlanet":"ForestHeart",
        "npcArchetype":   "Seated",
    },
    {
        "animalId":       "chipmunk",
        "displayName":    "Chipmunk",
        "element":        "Earth",
        "tier":           1,
        "behaviorMode":   "playful",
        "description":    "Quick and joyful earth spirit. Darts into movement at the first spark of joy.",
        "resonanceWeights": w(joy=0.55, movementFlow=0.30, socialSync=0.15),
        "bondThreshold":  0.27,
        "mythTrigger":    "forest",
        "preferredPlanet":"ForestHeart",
        "npcArchetype":   "FlowDancer",
    },
    {
        "animalId":       "snake",
        "displayName":    "Snake",
        "element":        "Earth",
        "tier":           2,
        "behaviorMode":   "ancient",
        "description":    "Ancient keeper of cycles. Appears when the player touches deep source alignment through stillness.",
        "resonanceWeights": w(sourceAlignment=0.50, calm=0.35, breathCoherence=0.15),
        "bondThreshold":  0.40,
        "mythTrigger":    "ruin",
        "preferredPlanet":"SourceVeil",
        "npcArchetype":   "Seated",
    },
    {
        "animalId":       "razorback_boar",
        "displayName":    "Razorback Boar",
        "element":        "Earth",
        "tier":           2,
        "behaviorMode":   "fierce",
        "description":    "Raw earth power. Bonds through forceful movement and spin — earth at its most assertive.",
        "resonanceWeights": w(spinStability=0.45, movementFlow=0.40, calm=0.15),
        "bondThreshold":  0.38,
        "mythTrigger":    "storm",
        "preferredPlanet":"ForestHeart",
        "npcArchetype":   "Dervish",
    },
    {
        "animalId":       "tiger",
        "displayName":    "Tiger",
        "element":        "Earth",
        "tier":           2,
        "behaviorMode":   "powerful-still",
        "description":    "Powerful stillness. Watches from shadow and bonds with those who hold calm alongside strength.",
        "resonanceWeights": w(calm=0.45, spinStability=0.35, sourceAlignment=0.20),
        "bondThreshold":  0.45,
        "mythTrigger":    "elder",
        "preferredPlanet":"ForestHeart",
        "npcArchetype":   "EarthDragon",
    },
    {
        "animalId":       "bison",
        "displayName":    "Bison",
        "element":        "Earth",
        "tier":           3,
        "behaviorMode":   "ancient-ground",
        "description":    "The oldest earth memory. Bonds through complete grounding — breath, calm, and source together sustained.",
        "resonanceWeights": w(calm=0.40, breathCoherence=0.30, sourceAlignment=0.30),
        "bondThreshold":  0.50,
        "mythTrigger":    "forest",
        "preferredPlanet":"ForestHeart",
        "npcArchetype":   "EarthDragon",
    },

    # =========================================================================
    # FOREST WORLD — ForestHeart / Tree spirits, temples, blessed rivers
    # These three companions are bound to the Forest World layer.
    # They appear near dryad trees, overgrown stone rings, Angkor temples,
    # and blessed rivers. They do not transfer to other realms.
    # =========================================================================
    {
        "animalId":       "parrot",
        "displayName":    "Forest Parrot",
        "element":        "Air",
        "tier":           1,
        "behaviorMode":   "mimic-joyful",
        "description":    "Vivid air spirit of the deep forest. Roosts in the causeway trees of ancient temples. Mimics the player's emotional state — it sounds like whatever you feel. Bonds through pure joy expressed freely in the presence of old things.",
        "resonanceWeights": w(joy=0.50, socialSync=0.30, movementFlow=0.20),
        "bondThreshold":  0.29,
        "mythTrigger":    "forest",
        "preferredPlanet":"ForestHeart",
        "npcArchetype":   "DryadTree",
    },
    {
        "animalId":       "forest_hawk",
        "displayName":    "Forest Hawk",
        "element":        "Air",
        "tier":           2,
        "behaviorMode":   "sacred-watch",
        "description":    "Nests in the moss-stone towers of ancient temples. Circles slowly overhead, always watching. Does not hunt in the player's presence — it observes. Bonds through the marriage of calm awareness and wonder: you must be still enough to notice without wanting to hold.",
        "resonanceWeights": w(calm=0.40, wonder=0.40, breathCoherence=0.20),
        "bondThreshold":  0.43,
        "mythTrigger":    "ruin",
        "preferredPlanet":"ForestHeart",
        "npcArchetype":   "DryadTree",
    },
    {
        "animalId":       "elder_white_stag",
        "displayName":    "Elder White Stag",
        "element":        "Earth",
        "tier":           3,
        "behaviorMode":   "sacred-ancient",
        "description":    "The oldest living thing in the forest. Pure white. Appears only at dawn, near the inner sanctum of the deepest temples or drinking from the source pools of blessed rivers. Does not bond quickly. The Stag does not come to you — you become still enough that it no longer needs to leave. The rarest earth bond. Requires source alignment, calm, and breath coherence all held together at depth.",
        "resonanceWeights": w(sourceAlignment=0.45, calm=0.35, breathCoherence=0.20),
        "bondThreshold":  0.58,
        "mythTrigger":    "elder",
        "preferredPlanet":"ForestHeart",
        "npcArchetype":   "DryadTree",
    },

    # =========================================================================
    # WATER — WaterFlow / WaterDragon realm
    # =========================================================================
    {
        "animalId":       "starfish",
        "displayName":    "Starfish",
        "element":        "Water",
        "tier":           1,
        "behaviorMode":   "gentle",
        "description":    "Silent regenerator. Appears in still water when calm and social sync are present together.",
        "resonanceWeights": w(calm=0.55, socialSync=0.30, breathCoherence=0.15),
        "bondThreshold":  0.27,
        "mythTrigger":    "ocean",
        "preferredPlanet":"WaterFlow",
        "npcArchetype":   "PairA",
    },
    {
        "animalId":       "gull",
        "displayName":    "Gull",
        "element":        "Water",
        "tier":           1,
        "behaviorMode":   "coastal",
        "description":    "Threshold creature of water and air. Responds to joyful flowing movement near the shore.",
        "resonanceWeights": w(movementFlow=0.50, joy=0.35, socialSync=0.15),
        "bondThreshold":  0.27,
        "mythTrigger":    "ocean",
        "preferredPlanet":"WaterFlow",
        "npcArchetype":   "FlowDancer",
    },
    {
        "animalId":       "pelican",
        "displayName":    "Pelican",
        "element":        "Water",
        "tier":           1,
        "behaviorMode":   "communal",
        "description":    "Generous community spirit. Bonds through social synchrony and calm shared presence.",
        "resonanceWeights": w(socialSync=0.50, calm=0.35, breathCoherence=0.15),
        "bondThreshold":  0.30,
        "mythTrigger":    "ocean",
        "preferredPlanet":"WaterFlow",
        "npcArchetype":   "PairB",
    },
    {
        "animalId":       "osprey",
        "displayName":    "Osprey",
        "element":        "Water",
        "tier":           2,
        "behaviorMode":   "precise-dive",
        "description":    "Bridge between sky and deep water. Bonds through movement precision and flow.",
        "resonanceWeights": w(movementFlow=0.45, spinStability=0.35, breathCoherence=0.20),
        "bondThreshold":  0.42,
        "mythTrigger":    "ocean",
        "preferredPlanet":"WaterFlow",
        "npcArchetype":   "FlowDancer",
    },
    {
        "animalId":       "dolphin",
        "displayName":    "Dolphin",
        "element":        "Water",
        "tier":           2,
        "behaviorMode":   "playful-wise",
        "description":    "Joyful deep intelligence. The most social water bond. Rewards joy expressed in community.",
        "resonanceWeights": w(joy=0.45, socialSync=0.40, movementFlow=0.15),
        "bondThreshold":  0.40,
        "mythTrigger":    "ocean",
        "preferredPlanet":"WaterFlow",
        "npcArchetype":   "WaterDragon",
    },
    {
        "animalId":       "octopus",
        "displayName":    "Octopus",
        "element":        "Water",
        "tier":           2,
        "behaviorMode":   "mysterious",
        "description":    "Deep pattern intelligence. Bonds through wonder and source — the creature that sees hidden connections.",
        "resonanceWeights": w(wonder=0.50, sourceAlignment=0.35, calm=0.15),
        "bondThreshold":  0.42,
        "mythTrigger":    "ruin",
        "preferredPlanet":"SourceVeil",
        "npcArchetype":   "WaterDragon",
    },
    {
        "animalId":       "whale",
        "displayName":    "Whale",
        "element":        "Water",
        "tier":           3,
        "behaviorMode":   "ancient-song",
        "description":    "Oldest ocean memory. Bonds only through deep breath coherence aligned with source. The bond of geological patience.",
        "resonanceWeights": w(sourceAlignment=0.40, breathCoherence=0.40, calm=0.20),
        "bondThreshold":  0.52,
        "mythTrigger":    "source",
        "preferredPlanet":"WaterFlow",
        "npcArchetype":   "WaterDragon",
    },
    {
        "animalId":       "kraken",
        "displayName":    "Kraken",
        "element":        "Water",
        "tier":           3,
        "behaviorMode":   "deep-mystery",
        "description":    "Ancient of the abyss. Not dark — vast. Bonds through wonder held steady at the edge of the unknown.",
        "resonanceWeights": w(wonder=0.50, sourceAlignment=0.35, breathCoherence=0.15),
        "bondThreshold":  0.55,
        "mythTrigger":    "elder",
        "preferredPlanet":"WaterFlow",
        "npcArchetype":   "WaterDragon",
    },

    # =========================================================================
    # FIRE — DarkContrast / FireDragon realm
    # =========================================================================
    {
        "animalId":       "jaguar",
        "displayName":    "Jaguar",
        "element":        "Fire",
        "tier":           1,
        "behaviorMode":   "precise",
        "description":    "Fast and accurate fire predator. Responds to spinning precision and directed movement.",
        "resonanceWeights": w(spinStability=0.50, movementFlow=0.35, joy=0.15),
        "bondThreshold":  0.34,
        "mythTrigger":    "fire",
        "preferredPlanet":"DarkContrast",
        "npcArchetype":   "Dervish",
    },
    {
        "animalId":       "panther",
        "displayName":    "Panther",
        "element":        "Fire",
        "tier":           1,
        "behaviorMode":   "shadow",
        "description":    "Shadow flame. Bonds through the paradox of calm power — stillness that contains intensity.",
        "resonanceWeights": w(calm=0.45, spinStability=0.40, sourceAlignment=0.15),
        "bondThreshold":  0.36,
        "mythTrigger":    "storm",
        "preferredPlanet":"DarkContrast",
        "npcArchetype":   "Dervish",
    },
    {
        "animalId":       "mountain_lion",
        "displayName":    "Mountain Lion",
        "element":        "Fire",
        "tier":           2,
        "behaviorMode":   "athletic",
        "description":    "Wild mountain fire. Bonds through flowing athletic movement and spin.",
        "resonanceWeights": w(movementFlow=0.45, spinStability=0.40, joy=0.15),
        "bondThreshold":  0.40,
        "mythTrigger":    "fire",
        "preferredPlanet":"DarkContrast",
        "npcArchetype":   "FireDragon",
    },
    {
        "animalId":       "lion",
        "displayName":    "Lion",
        "element":        "Fire",
        "tier":           2,
        "behaviorMode":   "regal",
        "description":    "Heart of the fire kingdom. Regal calm combined with raw power. Bonds through sovereign calm.",
        "resonanceWeights": w(calm=0.40, sourceAlignment=0.35, joy=0.25),
        "bondThreshold":  0.42,
        "mythTrigger":    "elder",
        "preferredPlanet":"DarkContrast",
        "npcArchetype":   "FireDragon",
    },
    {
        "animalId":       "griffin",
        "displayName":    "Griffin",
        "element":        "Fire",
        "tier":           2,
        "behaviorMode":   "noble",
        "description":    "Fire-air hybrid. Noble guardian spirit. Bonds through wonder held in precision — the mythic quality.",
        "resonanceWeights": w(wonder=0.45, spinStability=0.35, sourceAlignment=0.20),
        "bondThreshold":  0.44,
        "mythTrigger":    "fire",
        "preferredPlanet":"DarkContrast",
        "npcArchetype":   "FireDragon",
    },
    {
        "animalId":       "fire_drake",
        "displayName":    "Fire Drake",
        "element":        "Fire",
        "tier":           3,
        "behaviorMode":   "ancient-flame",
        "description":    "The original fire. Ancient, playful, terrifyingly alive. Bonds only when joy, wonder, and source alignment blaze together.",
        "resonanceWeights": w(joy=0.30, wonder=0.35, sourceAlignment=0.35),
        "bondThreshold":  0.55,
        "mythTrigger":    "fire",
        "preferredPlanet":"DarkContrast",
        "npcArchetype":   "FireDragon",
    },
]

# NPC archetype → default companion assignment
NPC_DEFAULT_COMPANIONS = {
    "Seated":        ["groundhog", "snake"],
    "FlowDancer":    ["dolphin", "gull"],
    "Dervish":       ["hawk", "raven"],
    "PairA":         ["macaw", "pelican"],
    "PairB":         ["macaw", "starfish"],
    "EarthDragon":   ["bison", "tiger"],
    "FireDragon":    ["fire_drake", "lion"],
    "WaterDragon":   ["whale", "kraken"],
    "ElderAirDragon":["harpy_eagle", "eagle"],
    # Forest World — parrot and hawk in temple/dryad zones
    # elder_white_stag is not assigned to NPCs; it appears on its own terms
    "DryadTree":     ["forest_hawk", "parrot"],
}


def main():
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)

    registry = {
        "companions":           COMPANIONS,
        "npcDefaultCompanions": NPC_DEFAULT_COMPANIONS,
    }

    OUT_PATH.write_text(json.dumps(registry, indent=2))
    print(f"Companion registry written to: {OUT_PATH}")
    print(f"Total companions: {len(COMPANIONS)}")

    # Summary table
    for element in ("Air", "Earth", "Water", "Fire"):
        group = [c for c in COMPANIONS if c["element"] == element]
        tiers = {1: [], 2: [], 3: []}
        for c in group:
            tiers[c["tier"]].append(c["displayName"])
        print(f"\n  {element} ({len(group)} companions):")
        for t, names in tiers.items():
            if names:
                print(f"    Tier {t}: {', '.join(names)}")


if __name__ == "__main__":
    main()
