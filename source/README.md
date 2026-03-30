# Spirits of the Crossing — Resonance Universe Engine

> *A mythology-driven AI simulation where ancient spirits inhabit living worlds, learn through resonance, and awaken myths when their collective coherence crosses sacred thresholds.*

---

## What this is

A Python simulation engine built from first principles around the **Upsilon Vibration Node** architecture — a field-driven AI system where autonomous agents respond to dynamic oscillatory signals rather than hard-coded rules.

The engine layers three systems on top of each other:

1. **The Field** — Each of six named worlds emits a real oscillatory field (Upsilon node) at its own elemental frequency. Agents physically sense the field: intensity, phase alignment, coherence, gradient toward the source.

2. **The Brain** — Each agent carries a 3-layer spherical oscillator brain (21 nodes: center → inner shell → outer Upsilon layer) with a fluid adaptive shell and an energy core. The brain's coupling weights **learn through Hebbian plasticity** — connections between co-active nodes strengthen when reinforcement is high.

3. **The Mythology** — Myths activate on each world when the collective resonance of its agents crosses thresholds derived from the game's design documents. The system tracks 20+ myth keys per world, from `forest(seedling)` to `elder(voyager)`. At step 5,200 — the Mayan Long Count cycle — **rebirth** fires universally across all worlds.

Behavior emerges from:
```
FIELD → PERCEPTION → BRAIN STATE → DECISION → ACTION → FIELD CHANGE
```
Agents feed back into the field. The field learns the agents. The myths record what rises from that.

---

## The Six Worlds

| World | Element | Mayan Correspondence | Upsilon Hz | Character |
|---|---|---|---|---|
| **ForestHeart** | Earth | Ixil / Ceiba world-tree | 0.25 | Lush, grounded, ancient memory |
| **SkySpiral** | Air | Ik wind glyphs | 0.55 | Aerial, exploratory, spinning |
| **SourceVeil** | Source | Xibalba threshold | 0.30 | Still, signal-rich, veil between worlds |
| **WaterFlow** | Water | Chaac rain serpent | 0.40 | Fluid, dynamic, social depth |
| **MachineOrder** | Machine | Itzamna celestial mechanics | 0.50 | Ordered, precise, cyclical |
| **DarkContrast** | Fire | Ah Puch dark passage | 0.65 | High challenge, transformative intensity |

---

## The Nine Archetypes (Nahuales)

Each agent is assigned a spirit archetype (Nahual — Mayan animal spirit guide) that shapes its brain constants and drive weights:

| Archetype | Nahual | Character | Brain effect |
|---|---|---|---|
| **Seated** | Ajaw (sun lord) | Still, grounded, crown-dominant | High COHERENCE_FLOOR, high identity gain |
| **FlowDancer** | Ik (wind) | Fluid, heart-blue axis | Balanced seek + explore |
| **Dervish** | Cauac (storm) | High spin, violet-blue | Low coherence floor, max explore gain |
| **PairA** | Ix (jaguar) | Social resonance, signal-dominant | Strong social match amplification |
| **PairB** | Lamat (rabbit) | Responsive, paired | Social with slight fear sensitivity |
| **EarthDragon** | Imix (earth crocodile) | Ancient, slow, rest-dominant | Highest coherence floor |
| **FireDragon** | Chicchan (serpent) | Intense, volatile | Fast rewiring under pressure |
| **WaterDragon** | Muluc (moon/water) | Fluid social depth | Balanced signal + explore |
| **ElderAirDragon** | Chuen (monkey) | Balanced all-band explorer | Strong reinforcement gain |

---

## Architecture

```
source/
  engine/
    snowflake/brain.py      ← 3-layer spherical brain + Hebbian learning
    simulation/
      star.py               ← energy emitter with solar flares
      planet.py             ← named world with world-bias + myth state
      universe.py           ← full simulation loop + Mayan cycle
    agents/agent.py         ← archetype-driven agent with field perception
    upsilon/
      node.py               ← oscillatory signal emitter (circular causality)
      field.py              ← NodeField + FieldSample perception
    interactions/
      actions.py            ← field-driven EMIT/STABILIZE/MOVE/REST
      encounters.py         ← pairwise resonance: ALLY/ORBIT/REPEL
      world_effects.py      ← agents imprint their planets
      spirit_animals.py     ← BALANCER / CATALYST / STABILIZER
    spirits/spirits.py      ← GUARDIAN / MAJOR / MINOR spirit hierarchy
    lore/
      planets.py            ← named world definitions + MAYAN_CYCLE
      archetypes.py         ← 9 Nahuales with drive weights
      myths.py              ← MythState: threshold tracking + tier system
    metrics/                ← sync, diversity, mean_energy
  experiments/
    run_viewer.py           ← live matplotlib: universe map + 3D brain
    run_headless.py         ← saves PNG snapshots (no display needed)
  requirements.txt
```

---

## Quick start

```bash
# 1. Clone or download this folder
# 2. Install dependencies
pip install -r requirements.txt

# 3. Run the live viewer (requires display)
python experiments/run_viewer.py

# 4. Run headless — saves PNG snapshots to output/
python experiments/run_headless.py
```

The live viewer shows:
- **Left panel**: 2D universe map with all six named worlds, orbiting Upsilon nodes (pulsing colour-coded circles), agents clustered by region, planet names with active myth tier markers
- **Right panel**: 3D brain of the highest-energy agent — snowflake nodes coloured by phase, translucent fluid shell ring, coupling edges

Terminal output every 100 steps:
```
step  100 | sync 0.082 | div 1.823 | avg_energy 12.4 | core 0.98 | shell 0.41
           myths: ForestHeart[ruin(e)]  SourceVeil[source(s) ruin(e)]  DarkContrast[fire(s) storm(s)]
```
Myth tiers: `(s)` = seedling, `(e)` = explorer, `(v)` = voyager

---

## The Mayan Long Count Cycle

At every **5,200 simulation steps** — one scaled Long Count cycle — the `rebirth` myth fires on all worlds simultaneously. This is the great cycle completing. Worlds that have been building toward `convergence` or `harmony` receive a surge. The cosmos remembers its age.

---

## The circular causality loop (self-regulation & learning)

Every tick, three feedback loops run simultaneously:

**Field → Agent**: The Upsilon node's signal raises the agent's `base_drive` when they are phase-aligned. A coherent field provides a reinforcement bonus — the agent is *rewarded* for resonating.

**Agent → Brain**: High reinforcement triggers Hebbian weight updates. The brain's coupling matrix drifts toward configurations that produced the reward — the agent literally rewires itself to hold the resonance longer.

**Agent → Field**: Energetic agents (energy > 2.5) amplify their planet's Upsilon node. High-sync agents nudge the node's phase toward their own brain's mean phase. Over time, the field and the agent become **mutually entrained** — the world remembers the spirits that inhabited it.

---

## Built from

- Upsilon Vibration Node AI System — Full Technical Design Document
- Spirits of the Crossing — Total Game Architecture
- SpiritsCrossing_Core/SpiritAI/spirit_profiles.json
- SpiritsCrossing_Core/SpiritAI/myth_thresholds.json
- Mayan cosmological calendar correspondence (Long Count cycle, day-sign Nahuales)
