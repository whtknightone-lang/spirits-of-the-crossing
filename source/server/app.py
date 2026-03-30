"""
Spirits of the Crossing — RUE Bridge Server
============================================
A FastAPI server that wraps the Universe simulation and exposes it
as a REST API for Unity (or any other client) to consume.

Unity sends player resonance data each tick and receives world state back.
The server maintains one live Universe instance.

Run:
    cd source/
    ../.venv/bin/uvicorn server.app:app --host 127.0.0.1 --port 8765 --reload

Endpoints:
    GET  /status          — health check
    GET  /state           — full current world state
    POST /step            — advance one tick (optional player resonance input)
    GET  /myths           — active myths per planet
    POST /reset           — restart the universe
"""

import sys
import os
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Dict, List, Optional
import numpy as np

from engine.simulation.universe import Universe
from engine.metrics.sync import sync
from engine.metrics.energy import mean_energy

app = FastAPI(title="Spirits of the Crossing — RUE Bridge", version="1.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

# ------------------------------------------------------------------
# Live universe instance
# ------------------------------------------------------------------

_universe: Optional[Universe] = None


def get_universe() -> Universe:
    global _universe
    if _universe is None:
        _universe = Universe(agents_per_planet=16, regions_per_planet=8)
    return _universe


# ------------------------------------------------------------------
# Input / Output models
# ------------------------------------------------------------------

class PlayerResonance(BaseModel):
    """
    Resonance state from the player — sent from Unity each tick.
    All fields are optional; omit what you haven't computed yet.
    """
    sync_level: float = 0.0          # player's current coherence (0-1)
    energy: float = 2.0              # player's energy state
    archetype: Optional[str] = None  # selected Nahual (e.g. "FlowDancer")
    planet_affinity: Optional[str] = None  # e.g. "SourceVeil"
    breath_phase: float = 0.0        # breath cycle phase (0 to 2π)


class NodeState(BaseModel):
    planet_id: int
    planet_name: str
    frequency: float
    amplitude: float
    phase: float
    pos_x: float
    pos_y: float


class AgentSummary(BaseModel):
    archetype: str
    planet_name: str
    energy: float
    brain_sync: float
    field_alignment: float


class PlanetState(BaseModel):
    index: int
    name: str
    element: str
    energy: float
    myth_summary: str
    myth_tier: Optional[str]
    active_myths: List[str]
    pos_x: float
    pos_y: float


class WorldState(BaseModel):
    age: int
    step: int
    star_energy: float
    global_sync: float
    global_energy: float
    core_energy: float
    shell_coherence: float
    planets: List[PlanetState]
    upsilon_nodes: List[NodeState]
    top_agents: List[AgentSummary]   # 3 highest-energy agents
    mayan_cycle_progress: float      # 0.0 to 1.0 within current cycle


# ------------------------------------------------------------------
# State serialiser
# ------------------------------------------------------------------

def _serialise_state(u: Universe) -> WorldState:
    phases = u.all_phases()

    planets = []
    for p in u.planets:
        px, py = p.pos
        planets.append(PlanetState(
            index=p.index,
            name=p.name,
            element=p.element,
            energy=round(float(p.energy), 3),
            myth_summary=p.myth_state.summary(),
            myth_tier=p.myth_state.highest_tier(),
            active_myths=sorted(p.myth_state.active),
            pos_x=round(float(px), 3),
            pos_y=round(float(py), 3),
        ))

    nodes = []
    for node in u.upsilon_nodes:
        nodes.append(NodeState(
            planet_id=node.planet_id,
            planet_name=u.planets[node.planet_id].name,
            frequency=round(float(node.frequency), 3),
            amplitude=round(float(node.amplitude), 3),
            phase=round(float(node.phase), 3),
            pos_x=round(float(node.pos[0]), 3),
            pos_y=round(float(node.pos[1]), 3),
        ))

    top_agents = sorted(u.agents, key=lambda a: a.energy, reverse=True)[:3]
    agent_summaries = [
        AgentSummary(
            archetype=a.archetype,
            planet_name=u.planets[a.planet_id].name,
            energy=round(float(a.energy), 2),
            brain_sync=round(float(a.brain.sync()), 3),
            field_alignment=round(float(a.last_field_alignment), 3),
        )
        for a in top_agents
    ]

    from engine.lore.planets import MAYAN_CYCLE
    cycle_progress = (u.age % MAYAN_CYCLE) / MAYAN_CYCLE

    return WorldState(
        age=u.age,
        step=u.time,
        star_energy=round(float(u.last_star_energy), 3),
        global_sync=round(float(sync(phases)), 4),
        global_energy=round(float(mean_energy(u.all_energies())), 2),
        core_energy=round(float(u.mean_core_energy()), 3),
        shell_coherence=round(float(u.mean_shell_coherence()), 3),
        planets=planets,
        upsilon_nodes=nodes,
        top_agents=agent_summaries,
        mayan_cycle_progress=round(float(cycle_progress), 4),
    )


# ------------------------------------------------------------------
# Endpoints
# ------------------------------------------------------------------

@app.get("/status")
def status():
    u = get_universe()
    return {"status": "running", "age": u.age, "planets": len(u.planets), "agents": len(u.agents)}


@app.get("/state", response_model=WorldState)
def state():
    """Full current world state — call after /step to get updated data."""
    return _serialise_state(get_universe())


@app.post("/step", response_model=WorldState)
def step(player: PlayerResonance = None):
    """
    Advance the universe one tick.
    Optionally inject player resonance to influence the simulation.

    If player.planet_affinity is set and matches a planet name,
    that planet's Upsilon node gets a coherence boost from the player.
    If player.archetype is set, the nearest agents on that planet
    receive a small reinforcement boost proportional to player.sync_level.
    """
    u = get_universe()

    if player:
        _inject_player_resonance(u, player)

    u.step()
    return _serialise_state(u)


@app.get("/myths")
def myths():
    """Just the myth states — lightweight for frequent polling."""
    u = get_universe()
    return {
        p.name: {
            "summary": p.myth_state.summary(),
            "tier": p.myth_state.highest_tier(),
            "active": sorted(p.myth_state.active),
            "scores": {k: round(v, 3) for k, v in p.myth_state.scores.items() if v > 0.01},
        }
        for p in u.planets
    }


@app.post("/reset")
def reset():
    """Restart the universe from scratch."""
    global _universe
    _universe = Universe(agents_per_planet=16, regions_per_planet=8)
    return {"status": "reset", "planets": len(_universe.planets)}


# ------------------------------------------------------------------
# Player resonance injection
# ------------------------------------------------------------------

def _inject_player_resonance(u: Universe, player: PlayerResonance) -> None:
    """
    Let the player's state ripple into the simulation.
    This is where the cave ritual output connects to the living world.
    """
    # Find the target planet
    target_planet = None
    if player.planet_affinity:
        for p in u.planets:
            if p.name == player.planet_affinity:
                target_planet = p
                break
    if target_planet is None:
        target_planet = u.planets[0]  # default to ForestHeart

    # Player's coherence nudges the planet's Upsilon node
    node = u.upsilon_nodes[target_planet.index]
    if player.sync_level > 0.3:
        node.amplitude = float(np.clip(node.amplitude + 0.01 * player.sync_level, 0.5, 2.5))
        # player breath phase entrains node phase
        diff = float(np.angle(np.exp(1j * (player.breath_phase - node.phase))))
        node.phase = float(np.mod(node.phase + 0.02 * diff, 2 * np.pi))

    # Player energy boosts nearby agents on the target planet
    if player.energy > 2.0:
        planet_agents = [a for a in u.agents if a.planet_id == target_planet.index]
        boost = 0.005 * min(player.energy, 5.0)
        for a in planet_agents[:8]:   # top 8 agents on planet
            a.energy += boost

    # If player selected a matching archetype, boost those agents more
    if player.archetype:
        matching = [a for a in u.agents
                    if a.planet_id == target_planet.index
                    and a.archetype == player.archetype]
        for a in matching:
            a.brain.core_energy = float(np.clip(
                a.brain.core_energy + 0.05 * player.sync_level, 0.0, 3.0
            ))
