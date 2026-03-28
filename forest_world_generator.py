"""
Forest World Generator
======================
Generates forest_world_data.json — the living data for the Forest World layer.

THE FOREST WORLD
  The forest exists outside the planetary orbit system. It is the oldest part
  of the game world — a place where the Source is closest to the surface.

  Three zones, each with its own character:

    The Deep Wood        — ancient trees, watching dryads, one sunken stone ring,
                           The Green Thread river. Entry zone.

    The Sunken Rings     — ceremonial ground. Three heavily overgrown stone rings
                           from The Root Age and Before the First Fire eras.
                           A tributary river (The Root Vein) with a source pool.
                           Dryads here are older and harder to reach.

    The Temple of the Crossing — Angkor-scale temple complex with causeway and
                           lotus moat. Five chambers, each with its own field.
                           The Elder White Stag appears at dawn in the sanctum.
                           Source Vein flows from beneath the moat.

DRYAD WHISPER LINES
  Written to be heard once, in stillness, near ancient trees.
  They never repeat. The dryad remembers what it has said.

Output: SpiritsCrossing_Core/ForestWorld/forest_world_data.json
"""

from __future__ import annotations
import json
import pathlib
import datetime

OUT_PATH = pathlib.Path(__file__).parent / "SpiritsCrossing_Core" / "ForestWorld" / "forest_world_data.json"

BANDS = ["red", "orange", "yellow", "green", "blue", "indigo", "violet"]

def field(r=0.2, o=0.2, y=0.3, g=0.6, b=0.4, i=0.5, v=0.5) -> dict:
    """Build a 7-band vibrational field. Earth/forest default: green-dominant, indigo/violet lifted."""
    return {
        "red": round(r, 3), "orange": round(o, 3), "yellow": round(y, 3),
        "green": round(g, 3), "blue": round(b, 3),
        "indigo": round(i, 3), "violet": round(v, 3),
    }


# =============================================================================
# ZONE 1: THE DEEP WOOD
# =============================================================================

DEEP_WOOD_DRYADS = [
    {
        "dryadId":            "dryad_old_oak_1",
        "treeId":             "OldOak_01",
        "forestZone":         "zone_deep_wood",
        "temperament":        "patient",
        "mythFragment":       "forest",
        "impressionThreshold": 0.33,
        "presenceThreshold":   0.70,
        "resonanceAffinity":  field(r=0.10, o=0.15, y=0.25, g=0.70, b=0.40, i=0.55, v=0.50),
        "whisperLines": [
            "I have been here longer than the stones.",
            "You are quieter than the last one who came.",
            "The rings remember. Walk toward the low ground.",
        ],
        "canGesture":   True,
        "bindsToRuin":  True,
        "boundRuinId":  "ring_deep_wood_01",
    },
    {
        "dryadId":            "dryad_old_oak_2",
        "treeId":             "OldOak_02",
        "forestZone":         "zone_deep_wood",
        "temperament":        "watching",
        "mythFragment":       "ruin",
        "impressionThreshold": 0.35,
        "presenceThreshold":   0.72,
        "resonanceAffinity":  field(r=0.12, o=0.18, y=0.28, g=0.65, b=0.38, i=0.60, v=0.55),
        "whisperLines": [
            "I watched you arrive.",
            "The others who came this way did not stay long enough.",
            "When you are ready, go deeper. The sunken rings will know you.",
        ],
        "canGesture":   True,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
    {
        "dryadId":            "dryad_cedar_1",
        "treeId":             "Cedar_01",
        "forestZone":         "zone_deep_wood",
        "temperament":        "joyful",
        "mythFragment":       "forest",
        "impressionThreshold": 0.30,
        "presenceThreshold":   0.65,
        "resonanceAffinity":  field(r=0.20, o=0.35, y=0.50, g=0.70, b=0.35, i=0.40, v=0.40),
        "whisperLines": [
            "Oh! You can feel us.",
            "The parrots knew before you did.",
        ],
        "canGesture":   True,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
    {
        "dryadId":            "dryad_cedar_2",
        "treeId":             "Cedar_02",
        "forestZone":         "zone_deep_wood",
        "temperament":        "grieving",
        "mythFragment":       "ruin",
        "impressionThreshold": 0.38,
        "presenceThreshold":   0.75,
        "resonanceAffinity":  field(r=0.08, o=0.10, y=0.20, g=0.55, b=0.45, i=0.65, v=0.60),
        "whisperLines": [
            "My tree was twice this size, once.",
            "I remember the ring when it was whole. Before the roots took it.",
            "Do not grieve for what the forest takes. It gives it back differently.",
        ],
        "canGesture":   False,
        "bindsToRuin":  True,
        "boundRuinId":  "ring_deep_wood_01",
    },
    {
        "dryadId":            "dryad_elder_ash",
        "treeId":             "ElderAsh_01",
        "forestZone":         "zone_deep_wood",
        "temperament":        "ancient",
        "mythFragment":       "elder",
        "impressionThreshold": 0.42,
        "presenceThreshold":   0.80,
        "resonanceAffinity":  field(r=0.05, o=0.08, y=0.15, g=0.60, b=0.50, i=0.80, v=0.85),
        "whisperLines": [
            "You have breath coherence. That is rare now.",
            "The stag and I have been here since before the temple had a name.",
            "Stay until the light changes. Then you will understand why we watch.",
            "The rings below were built by people who knew what we are. They left when the knowing went quiet.",
        ],
        "canGesture":   True,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
    {
        "dryadId":            "dryad_twisted_yew",
        "treeId":             "TwistedYew_01",
        "forestZone":         "zone_deep_wood",
        "temperament":        "fierce",
        "mythFragment":       "storm",
        "impressionThreshold": 0.40,
        "presenceThreshold":   0.78,
        "resonanceAffinity":  field(r=0.15, o=0.20, y=0.30, g=0.55, b=0.35, i=0.55, v=0.50),
        "whisperLines": [
            "Not all trees are welcoming. I am not.",
            "You are still here. That earns you one more moment.",
        ],
        "canGesture":   False,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
    {
        "dryadId":            "dryad_birch_1",
        "treeId":             "Birch_01",
        "forestZone":         "zone_deep_wood",
        "temperament":        "curious",
        "mythFragment":       "forest",
        "impressionThreshold": 0.28,
        "presenceThreshold":   0.60,
        "resonanceAffinity":  field(r=0.18, o=0.30, y=0.45, g=0.65, b=0.40, i=0.42, v=0.38),
        "whisperLines": [
            "You are the second one this season.",
            "The hawk circles you. That is good.",
        ],
        "canGesture":   True,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
    {
        "dryadId":            "dryad_willow_1",
        "treeId":             "Willow_01",
        "forestZone":         "zone_deep_wood",
        "temperament":        "gentle",
        "mythFragment":       "ocean",
        "impressionThreshold": 0.25,
        "presenceThreshold":   0.62,
        "resonanceAffinity":  field(r=0.10, o=0.18, y=0.30, g=0.72, b=0.55, i=0.50, v=0.48),
        "whisperLines": [
            "The river runs beneath my roots. I drink source water.",
            "Follow the green thread when you are lost.",
        ],
        "canGesture":   True,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
]

DEEP_WOOD_RINGS = [
    {
        "ringId":             "ring_deep_wood_01",
        "forestZone":         "zone_deep_wood",
        "stoneCount":         7,
        "ringRadiusMeters":   5.5,
        "overgrowthLevel":    0.75,
        "frozenField":        field(r=0.08, o=0.12, y=0.22, g=0.68, b=0.42, i=0.62, v=0.70),
        "discoveryThreshold": 0.58,
        "mythTrigger":        "forest",
        "era":                "The Stone Dreaming",
        "nearbyDryadIds":     ["dryad_old_oak_1", "dryad_cedar_2"],
        "hasAltarStone":      True,
        "altarResonanceBonus": 0.08,
        "centerMythTrigger":  "ruin",
    },
]

DEEP_WOOD_RIVERS = [
    {
        "riverId":             "river_green_thread",
        "riverName":           "The Green Thread",
        "forestZone":          "zone_deep_wood",
        "segmentCount":        8,
        "totalLengthMeters":   450.0,
        "hasSourcePool":       False,
        "hasSeaExit":          True,
        "greenAmplification":  0.040,
        "violetAmplification": 0.020,
        "adjacencyRadius":     6.0,
        "stagDrinksAtSource":  False,
        "mythTrigger":         "forest",
        "sourcePoolMythTrigger": "",
        "riverField":          field(r=0.08, o=0.12, y=0.28, g=0.80, b=0.50, i=0.45, v=0.42),
    },
]

DEEP_WOOD_ZONE = {
    "zoneId":   "zone_deep_wood",
    "zoneName": "The Deep Wood",
    "element":  "Earth",
    "ambientField": field(r=0.12, o=0.18, y=0.30, g=0.65, b=0.42, i=0.50, v=0.48),
    "dryads":       DEEP_WOOD_DRYADS,
    "stoneRings":   DEEP_WOOD_RINGS,
    "temples":      [],
    "rivers":       DEEP_WOOD_RIVERS,
    "preferredCompanionIds": ["parrot", "forest_hawk"],
}


# =============================================================================
# ZONE 2: THE SUNKEN RINGS
# =============================================================================

SUNKEN_RING_DRYADS = [
    {
        "dryadId":            "dryad_ring_oak_1",
        "treeId":             "RingOak_01",
        "forestZone":         "zone_sunken_rings",
        "temperament":        "ancient",
        "mythFragment":       "ruin",
        "impressionThreshold": 0.45,
        "presenceThreshold":   0.82,
        "resonanceAffinity":  field(r=0.05, o=0.08, y=0.15, g=0.55, b=0.45, i=0.75, v=0.80),
        "whisperLines": [
            "These rings were here before trees were.",
            "The ones who built them understood silence.",
            "You have found the first ring. There are two more. They are not waiting for you — but they will know you came.",
        ],
        "canGesture":   True,
        "bindsToRuin":  True,
        "boundRuinId":  "ring_root_age_01",
    },
    {
        "dryadId":            "dryad_ring_oak_2",
        "treeId":             "RingOak_02",
        "forestZone":         "zone_sunken_rings",
        "temperament":        "patient",
        "mythFragment":       "ruin",
        "impressionThreshold": 0.42,
        "presenceThreshold":   0.80,
        "resonanceAffinity":  field(r=0.06, o=0.10, y=0.18, g=0.58, b=0.48, i=0.72, v=0.75),
        "whisperLines": [
            "I have seen many seasons. You are small in all of them. That is not an insult.",
            "The second ring is older. Its stones go deeper.",
        ],
        "canGesture":   True,
        "bindsToRuin":  True,
        "boundRuinId":  "ring_root_age_02",
    },
    {
        "dryadId":            "dryad_altar_oak",
        "treeId":             "AltarOak_01",
        "forestZone":         "zone_sunken_rings",
        "temperament":        "watchful",
        "mythFragment":       "elder",
        "impressionThreshold": 0.50,
        "presenceThreshold":   0.85,
        "resonanceAffinity":  field(r=0.04, o=0.07, y=0.12, g=0.52, b=0.50, i=0.82, v=0.90),
        "whisperLines": [
            "You are standing on the altar stone of the first ring. Good.",
            "This is where they came to listen for Source.",
            "The third ring, the buried one, is older than this altar. It predates the name for fire.",
            "When you are ready for the temple, the hawk will circle it. Follow where it does not land.",
        ],
        "canGesture":   True,
        "bindsToRuin":  True,
        "boundRuinId":  "ring_root_age_01",
    },
    {
        "dryadId":            "dryad_ring_pine",
        "treeId":             "RingPine_01",
        "forestZone":         "zone_sunken_rings",
        "temperament":        "grieving",
        "mythFragment":       "forest",
        "impressionThreshold": 0.36,
        "presenceThreshold":   0.74,
        "resonanceAffinity":  field(r=0.08, o=0.12, y=0.20, g=0.58, b=0.42, i=0.65, v=0.68),
        "whisperLines": [
            "The people who knew these rings are gone.",
            "But the rings themselves are fine. They do not need to be remembered to hold their field.",
        ],
        "canGesture":   False,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
]

SUNKEN_RING_RINGS = [
    {
        "ringId":             "ring_root_age_01",
        "forestZone":         "zone_sunken_rings",
        "stoneCount":         11,
        "ringRadiusMeters":   8.0,
        "overgrowthLevel":    0.90,
        "frozenField":        field(r=0.06, o=0.10, y=0.18, g=0.60, b=0.44, i=0.72, v=0.78),
        "discoveryThreshold": 0.62,
        "mythTrigger":        "ruin",
        "era":                "The Root Age",
        "nearbyDryadIds":     ["dryad_ring_oak_1", "dryad_altar_oak"],
        "hasAltarStone":      True,
        "altarResonanceBonus": 0.10,
        "centerMythTrigger":  "elder",
    },
    {
        "ringId":             "ring_root_age_02",
        "forestZone":         "zone_sunken_rings",
        "stoneCount":         9,
        "ringRadiusMeters":   6.5,
        "overgrowthLevel":    0.85,
        "frozenField":        field(r=0.05, o=0.09, y=0.16, g=0.58, b=0.46, i=0.70, v=0.74),
        "discoveryThreshold": 0.62,
        "mythTrigger":        "ruin",
        "era":                "The Root Age",
        "nearbyDryadIds":     ["dryad_ring_oak_2"],
        "hasAltarStone":      False,
        "altarResonanceBonus": 0.0,
        "centerMythTrigger":  "",
    },
    {
        "ringId":             "ring_first_age_01",
        "forestZone":         "zone_sunken_rings",
        "stoneCount":         5,
        "ringRadiusMeters":   4.0,
        "overgrowthLevel":    0.97,
        "frozenField":        field(r=0.04, o=0.06, y=0.12, g=0.50, b=0.48, i=0.80, v=0.90),
        "discoveryThreshold": 0.70,
        "mythTrigger":        "elder",
        "era":                "Before the First Fire",
        "nearbyDryadIds":     [],
        "hasAltarStone":      False,
        "altarResonanceBonus": 0.0,
        "centerMythTrigger":  "source",
        # Stones at overgrowthLevel 0.97: mostly underground. Three are visible only
        # as raised moss mounds. Two lean at extreme angles, cracked but standing.
        # No altar stone — this ring predates the concept of altars.
    },
]

SUNKEN_RING_RIVERS = [
    {
        "riverId":             "river_root_vein",
        "riverName":           "The Root Vein",
        "forestZone":          "zone_sunken_rings",
        "segmentCount":        5,
        "totalLengthMeters":   280.0,
        "hasSourcePool":       True,
        "hasSeaExit":          False,
        "greenAmplification":  0.050,
        "violetAmplification": 0.030,
        "adjacencyRadius":     5.0,
        "stagDrinksAtSource":  False,
        "mythTrigger":         "forest",
        "sourcePoolMythTrigger": "ruin",
        "riverField":          field(r=0.06, o=0.10, y=0.22, g=0.75, b=0.48, i=0.58, v=0.62),
    },
]

SUNKEN_RINGS_ZONE = {
    "zoneId":   "zone_sunken_rings",
    "zoneName": "The Sunken Rings",
    "element":  "Earth",
    "ambientField": field(r=0.08, o=0.12, y=0.20, g=0.58, b=0.46, i=0.68, v=0.72),
    "dryads":       SUNKEN_RING_DRYADS,
    "stoneRings":   SUNKEN_RING_RINGS,
    "temples":      [],
    "rivers":       SUNKEN_RING_RIVERS,
    "preferredCompanionIds": ["forest_hawk", "elder_white_stag"],
}


# =============================================================================
# ZONE 3: THE TEMPLE OF THE CROSSING
# =============================================================================

TEMPLE_DRYADS = [
    {
        "dryadId":            "dryad_causeway_elm",
        "treeId":             "CausewayElm_01",
        "forestZone":         "zone_temple_crossing",
        "temperament":        "patient",
        "mythFragment":       "ruin",
        "impressionThreshold": 0.40,
        "presenceThreshold":   0.75,
        "resonanceAffinity":  field(r=0.08, o=0.12, y=0.22, g=0.62, b=0.42, i=0.65, v=0.68),
        "whisperLines": [
            "The causeway was built so the approach itself was a prayer.",
            "Walk slowly. The stones were placed at specific intervals. There is a rhythm.",
        ],
        "canGesture":   True,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
    {
        "dryadId":            "dryad_causeway_elm_2",
        "treeId":             "CausewayElm_02",
        "forestZone":         "zone_temple_crossing",
        "temperament":        "watching",
        "mythFragment":       "forest",
        "impressionThreshold": 0.38,
        "presenceThreshold":   0.73,
        "resonanceAffinity":  field(r=0.10, o=0.15, y=0.25, g=0.60, b=0.40, i=0.62, v=0.65),
        "whisperLines": [
            "The parrots have roosted here for as long as the temple has been ruined.",
            "Before it was ruined, they roosted here too.",
        ],
        "canGesture":   False,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
    {
        "dryadId":            "dryad_entrance_column",
        "treeId":             "EntranceColumn_Root_01",
        "forestZone":         "zone_temple_crossing",
        "temperament":        "ancient",
        "mythFragment":       "ruin",
        "impressionThreshold": 0.45,
        "presenceThreshold":   0.80,
        # Temple dryads live in root-wrapped columns, not trees.
        # Their resonanceAffinity is stronger in indigo/violet (deeper structure).
        "resonanceAffinity":  field(r=0.05, o=0.08, y=0.14, g=0.55, b=0.48, i=0.78, v=0.82),
        "whisperLines": [
            "This root has been growing through this column for four hundred years.",
            "I grew with it.",
            "The second chamber has a still pool. The water does not move. Something old is at the bottom.",
        ],
        "canGesture":   True,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
    {
        "dryadId":            "dryad_root_hall_column",
        "treeId":             "RootHallColumn_01",
        "forestZone":         "zone_temple_crossing",
        "temperament":        "ancient",
        "mythFragment":       "elder",
        "impressionThreshold": 0.52,
        "presenceThreshold":   0.83,
        "resonanceAffinity":  field(r=0.04, o=0.06, y=0.12, g=0.52, b=0.50, i=0.82, v=0.88),
        "whisperLines": [
            "The root hall is what remains when a building and a forest become the same thing.",
            "Notice which direction the roots come from. That direction is the oldest part of this place.",
            "Go deeper. The stag has been in the inner court before dawn. It may come again.",
        ],
        "canGesture":   True,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
    {
        "dryadId":            "dryad_inner_court_fig",
        "treeId":             "InnerCourtFig_01",
        "forestZone":         "zone_temple_crossing",
        "temperament":        "sacred",
        "mythFragment":       "source",
        "impressionThreshold": 0.55,
        "presenceThreshold":   0.85,
        "resonanceAffinity":  field(r=0.03, o=0.05, y=0.10, g=0.50, b=0.52, i=0.85, v=0.92),
        "whisperLines": [
            "You have come to the inner court.",
            "The stag stands here at first light, facing east.",
            "It does not acknowledge anything that approaches.",
            "It simply stands. And somehow that is enough for everything in this place.",
        ],
        "canGesture":   True,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
    {
        "dryadId":            "dryad_sanctum_banyan",
        "treeId":             "SanctumBanyan_01",
        "forestZone":         "zone_temple_crossing",
        "temperament":        "source",
        "mythFragment":       "source",
        "impressionThreshold": 0.60,
        "presenceThreshold":   0.88,
        "resonanceAffinity":  field(r=0.02, o=0.04, y=0.08, g=0.48, b=0.55, i=0.88, v=0.95),
        "whisperLines": [
            "This is the sanctum.",
            "The original builders left something here that is not physical.",
            "You are breathing it right now.",
        ],
        "canGesture":   False,
        "bindsToRuin":  False,
        "boundRuinId":  "",
    },
]

TEMPLE_CHAMBERS = [
    {
        "chamberId":          "chamber_entrance_hall",
        "chamberName":        "The Entrance Hall",
        "discoveryThreshold": 0.45,
        "mythTrigger":        "ruin",
        "chamberField":       field(r=0.12, o=0.18, y=0.28, g=0.60, b=0.40, i=0.58, v=0.60),
        "rootDensity":        0.30,
        "vineCoverage":       0.55,
        "hasOpenRoof":        False,
        "chamberDryadIds":    ["dryad_entrance_column"],
    },
    {
        "chamberId":          "chamber_root_hall",
        "chamberName":        "The Root Hall",
        "discoveryThreshold": 0.55,
        "mythTrigger":        "ruin",
        "chamberField":       field(r=0.08, o=0.12, y=0.20, g=0.58, b=0.44, i=0.70, v=0.72),
        "rootDensity":        0.85,
        "vineCoverage":       0.40,
        "hasOpenRoof":        True,   # roots pushed through — light streams in
        "chamberDryadIds":    ["dryad_root_hall_column"],
    },
    {
        "chamberId":          "chamber_still_water",
        "chamberName":        "Chamber of Still Water",
        "discoveryThreshold": 0.60,
        "mythTrigger":        "ocean",
        "chamberField":       field(r=0.06, o=0.10, y=0.18, g=0.62, b=0.58, i=0.72, v=0.75),
        "rootDensity":        0.60,
        "vineCoverage":       0.70,
        "hasOpenRoof":        False,
        "chamberDryadIds":    [],
    },
    {
        "chamberId":          "chamber_inner_court",
        "chamberName":        "The Inner Court",
        "discoveryThreshold": 0.65,
        "mythTrigger":        "elder",
        "chamberField":       field(r=0.04, o=0.07, y=0.14, g=0.55, b=0.50, i=0.80, v=0.85),
        "rootDensity":        0.70,
        "vineCoverage":       0.50,
        "hasOpenRoof":        True,
        "chamberDryadIds":    ["dryad_inner_court_fig"],
    },
    {
        "chamberId":          "chamber_sanctum",
        "chamberName":        "The Sanctum",
        "discoveryThreshold": 0.72,
        "mythTrigger":        "source",
        "chamberField":       field(r=0.02, o=0.04, y=0.10, g=0.50, b=0.55, i=0.88, v=0.95),
        "rootDensity":        0.95,
        "vineCoverage":       0.35,
        "hasOpenRoof":        True,
        "chamberDryadIds":    ["dryad_sanctum_banyan"],
    },
]

TEMPLE_RECORD = {
    "templeId":                "temple_of_the_crossing",
    "forestZone":              "zone_temple_crossing",
    "templeStyle":             "angkor",
    "chambers":                TEMPLE_CHAMBERS,
    "overallRootDensity":      0.70,
    "hasCauseway":             True,
    "hasMoat":                 True,   # still black water + lotus
    "templeField":             field(r=0.05, o=0.08, y=0.15, g=0.55, b=0.50, i=0.78, v=0.82),
    "outerDiscoveryThreshold": 0.45,
    "innerDiscoveryThreshold": 0.70,
    "outerMythTrigger":        "ruin",
    "innerMythTrigger":        "elder",
    "hawkNestPresent":         True,
    "parrotRoostPresent":      True,
    "stagAppearsAtDawn":       True,
}

TEMPLE_CROSSING_RINGS = [
    {
        "ringId":             "ring_temple_outer",
        "forestZone":         "zone_temple_crossing",
        "stoneCount":         13,
        "ringRadiusMeters":   10.0,
        "overgrowthLevel":    0.60,
        # Outer ritual ring. Predates the temple. The temple was built around it.
        # Stones are large, upright, less buried than the Sunken Rings.
        "frozenField":        field(r=0.07, o=0.11, y=0.20, g=0.60, b=0.46, i=0.68, v=0.72),
        "discoveryThreshold": 0.55,
        "mythTrigger":        "ruin",
        "era":                "The Root Age",
        "nearbyDryadIds":     ["dryad_causeway_elm"],
        "hasAltarStone":      True,
        "altarResonanceBonus": 0.09,
        "centerMythTrigger":  "elder",
    },
]

TEMPLE_RIVERS = [
    {
        "riverId":             "river_source_vein",
        "riverName":           "Source Vein",
        "forestZone":          "zone_temple_crossing",
        "segmentCount":        4,
        "totalLengthMeters":   200.0,
        "hasSourcePool":       True,
        # Source pool is beneath the temple's inner sanctum, at the base of the banyan.
        "hasSeaExit":          False,
        "greenAmplification":  0.045,
        "violetAmplification": 0.040,
        "adjacencyRadius":     8.0,   # widest — the most sacred river
        "stagDrinksAtSource":  True,  # Elder White Stag drinks from Source Vein's pool
        "mythTrigger":         "forest",
        "sourcePoolMythTrigger": "source",
        "riverField":          field(r=0.03, o=0.05, y=0.12, g=0.65, b=0.55, i=0.80, v=0.90),
    },
]

TEMPLE_CROSSING_ZONE = {
    "zoneId":   "zone_temple_crossing",
    "zoneName": "The Temple of the Crossing",
    "element":  "Source",
    "ambientField": field(r=0.05, o=0.08, y=0.16, g=0.56, b=0.52, i=0.76, v=0.80),
    "dryads":       TEMPLE_DRYADS,
    "stoneRings":   TEMPLE_CROSSING_RINGS,
    "temples":      [TEMPLE_RECORD],
    "rivers":       TEMPLE_RIVERS,
    "preferredCompanionIds": ["parrot", "forest_hawk", "elder_white_stag"],
}


# =============================================================================
# MAIN
# =============================================================================

def main():
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)

    forest_data = {
        "worldId":   "forest_world",
        "worldName": "The Forest of the Crossing",
        "zones": [DEEP_WOOD_ZONE, SUNKEN_RINGS_ZONE, TEMPLE_CROSSING_ZONE],
        "generatedAt": datetime.datetime.utcnow().isoformat() + "Z",
    }

    OUT_PATH.write_text(json.dumps(forest_data, indent=2))

    total_dryads     = sum(len(z["dryads"])    for z in forest_data["zones"])
    total_rings      = sum(len(z["stoneRings"]) for z in forest_data["zones"])
    total_temples    = sum(len(z["temples"])   for z in forest_data["zones"])
    total_chambers   = sum(len(t["chambers"])  for z in forest_data["zones"] for t in z["temples"])
    total_rivers     = sum(len(z["rivers"])    for z in forest_data["zones"])

    print("Forest World Generator")
    print("=" * 60)
    print(f"{'Zone':<28} {'Dryads':>6} {'Rings':>6} {'Temples':>8} {'Rivers':>7}")
    print("-" * 60)
    for z in forest_data["zones"]:
        print(f"  {z['zoneName']:<26} {len(z['dryads']):>6} "
              f"{len(z['stoneRings']):>6} {len(z['temples']):>8} {len(z['rivers']):>7}")
    print("-" * 60)
    print(f"  {'TOTAL':<26} {total_dryads:>6} {total_rings:>6} "
          f"{total_temples:>8} ({total_chambers} chambers) {total_rivers:>7}")
    print(f"\nWritten to: {OUT_PATH}")


if __name__ == "__main__":
    main()
