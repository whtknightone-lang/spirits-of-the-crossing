import numpy as np

from engine.simulation.star import Star
from engine.simulation.planet import Planet
from engine.agents.agent import Agent
from engine.interactions.actions import choose_action
from engine.interactions.encounters import resolve_encounter
from engine.interactions.world_effects import apply_world_effect
from engine.interactions.spirit_animals import SpiritAnimal
from engine.upsilon.node import UpsilonNode
from engine.upsilon.field import NodeField
from engine.lore.planets import PLANET_LORE, MAYAN_CYCLE


def _circular_diff(a: float, b: float) -> float:
    return float(np.angle(np.exp(1j * (a - b))))


class Universe:
    """
    Full RUE simulation.

    Each tick (step):
      1. Star emits energy
      2. Planets orbit and update regional fields
      3. Agents sense local phase/energy, step their brains, earn reward/penalty
      4. Agents migrate toward best-scoring regions
      5. Interaction layer: actions, encounters, world effects, spirit animals
    """

    def __init__(
        self,
        num_planets: int = None,
        agents_per_planet: int = 16,
        regions_per_planet: int = 8,
    ):
        # default to all 6 named worlds; caller can pass fewer for testing
        lore_list = PLANET_LORE[: num_planets] if num_planets else PLANET_LORE
        num_planets = len(lore_list)

        self.star = Star()
        self.planets = [
            Planet(i, radius=2.5 + i * 1.8, n_regions=regions_per_planet, lore=lore_list[i])
            for i in range(num_planets)
        ]
        self.agents = []
        for p in self.planets:
            pref = p.lore.preferred_archetypes if p.lore else [None]
            for j in range(agents_per_planet):
                archetype = pref[j % len(pref)]
                self.agents.append(
                    Agent(planet_id=p.index, region_id=j % regions_per_planet,
                          archetype=archetype)
                )
        self.time = 0
        self.age = 0            # universe age counter (for Mayan cycle)
        self.last_star_energy = 0.0

        # spirit animals — field-level regulators
        self.spirit_animals = [
            SpiritAnimal("BALANCER"),
            SpiritAnimal("CATALYST"),
            SpiritAnimal("STABILIZER"),
        ]

        # Upsilon nodes — one per planet, frequency from planet lore
        self.upsilon_nodes = [
            UpsilonNode(
                world_pos=list(self.planets[i].pos),
                frequency=lore_list[i].upsilon_frequency,
                amplitude=1.0,
                influence_radius=3.5 + 0.3 * i,
                planet_id=i,
                orbit_r=0.9,
                orbit_offset=np.pi / 3 * i,
            )
            for i in range(num_planets)
        ]
        self.node_field = NodeField(self.upsilon_nodes)

    # ------------------------------------------------------------------
    # Internal helpers
    # ------------------------------------------------------------------

    def _planet_agents(self, planet_id: int):
        return [a for a in self.agents if a.planet_id == planet_id]

    def _social_match_for_agent(self, agent, neighbors):
        if not neighbors:
            return 0.0
        sig = agent.signature()
        vals = [
            np.cos(_circular_diff(sig, other.signature()))
            for other in neighbors
            if other is not agent
        ]
        return float(np.mean(vals)) if vals else 0.0

    def _move_agents(self):
        """Agents migrate to the region that scores best for them."""
        for p in self.planets:
            planet_agents = self._planet_agents(p.index)
            for agent in planet_agents:
                current = agent.region_id
                scores = []
                for rid in range(p.n_regions):
                    local_phase = p.region_phases[rid]
                    local_energy = p.region_energies[rid]
                    match = np.cos(_circular_diff(agent.signature(), local_phase))

                    peers = [
                        a for a in planet_agents if a.region_id == rid and a is not agent
                    ]
                    peer_match = (
                        float(
                            np.mean(
                                [
                                    np.cos(_circular_diff(agent.signature(), peer.signature()))
                                    for peer in peers
                                ]
                            )
                        )
                        if peers
                        else 0.0
                    )

                    crowd_penalty = 0.03 * len(peers)
                    score = (
                        0.70 * match
                        + 0.25 * peer_match
                        + 0.20 * (local_energy / max(0.5, p.energy))
                        - crowd_penalty
                    )
                    scores.append(score)

                best_region = int(np.argmax(scores))
                if best_region != current and scores[best_region] > scores[current] + 0.08:
                    agent.region_id = best_region

    def _interaction_step(self):
        """
        Actions → encounters → world effects → spirit animals.
        Runs after agents have already stepped their brains this tick.
        """
        # 1. per-agent actions — mismatch now derived from real field alignment
        for a in self.agents:
            coherence = a.brain.sync()
            mismatch = 1.0 - max(0.0, a.last_field_alignment)  # field-driven
            action = choose_action(a, coherence, mismatch, a.energy)
            if action == "EMIT":
                a.energy += 0.02
            elif action == "STABILIZE":
                a.energy += 0.01

        # 2. pairwise encounters within same planet
        for i in range(len(self.agents)):
            for j in range(i + 1, len(self.agents)):
                if self.agents[i].planet_id == self.agents[j].planet_id:
                    resolve_encounter(self.agents[i], self.agents[j])

        # 3. agents modify their host planet
        for a in self.agents:
            apply_world_effect(a, self.planets[a.planet_id])

        # 4. spirit animals regulate the field
        for s in self.spirit_animals:
            s.act(self)

    # ------------------------------------------------------------------
    # Main tick
    # ------------------------------------------------------------------

    def _update_upsilon_nodes(self) -> None:
        """Reposition each node to orbit its planet, then advance its phase."""
        for node in self.upsilon_nodes:
            p = self.planets[node.planet_id]
            orbit_angle = p.angle + node.orbit_offset
            node.pos = np.array([
                p.pos[0] + node.orbit_r * np.cos(orbit_angle),
                p.pos[1] + node.orbit_r * np.sin(orbit_angle),
            ])
            node.step(dt=0.01)

    def step(self):
        self.time += 1
        self.last_star_energy = self.star.emit_energy()

        # update environment
        for p in self.planets:
            p.update_orbit()
            p.update_regions(self.last_star_energy)

        # update Upsilon nodes (orbit + phase advance)
        self._update_upsilon_nodes()

        # agent brain steps with field perception
        for p in self.planets:
            planet_agents = self._planet_agents(p.index)
            for agent in planet_agents:
                neighbors = [a for a in planet_agents if a.region_id == agent.region_id]
                social_match = self._social_match_for_agent(agent, neighbors)
                local_phase = p.region_phases[agent.region_id]
                local_energy = p.region_energies[agent.region_id]

                # sample Upsilon field at this agent's world position
                agent_world_pos = p.region_world_pos(agent.region_id)
                field_sample = self.node_field.sample(agent_world_pos, agent.signature())

                agent.step_with_field(
                    local_phase=local_phase,
                    local_energy=local_energy,
                    social_match=social_match,
                    field_sample=field_sample,
                )

        # circular causality: agents feed back into nearest node
        for agent in self.agents:
            p = self.planets[agent.planet_id]
            nearest_node = self.upsilon_nodes[agent.planet_id]
            nearest_node.receive_feedback(
                agent_phase=agent.signature(),
                agent_energy=agent.energy,
                agent_sync=agent.brain.sync(),
            )

        self._move_agents()
        self._interaction_step()
        self._update_myths()

    def _update_myths(self) -> None:
        """Update myth states for all planets, fire Mayan cycle rebirth if due."""
        self.age += 1
        mayan_turn = (self.age % MAYAN_CYCLE == 0)

        for p in self.planets:
            planet_agents = self._planet_agents(p.index)
            if planet_agents:
                mean_sync = float(np.mean([a.brain.sync() for a in planet_agents]))
            else:
                mean_sync = 0.0

            p.myth_state.update(mean_sync)

            # Mayan Long Count turn — rebirth myth fires on all planets
            if mayan_turn:
                p.myth_state.force_activate("rebirth", score=0.85)
                print(f"  *** MAYAN CYCLE TURNS (age {self.age}) — rebirth awakens on {p.name} ***")

    # ------------------------------------------------------------------
    # Metrics
    # ------------------------------------------------------------------

    def all_phases(self):
        return np.concatenate([a.brain.phases for a in self.agents])

    def all_energies(self):
        return np.array([a.energy for a in self.agents], dtype=float)

    def mean_core_energy(self):
        return float(np.mean([a.brain.core_energy for a in self.agents]))

    def mean_shell_coherence(self):
        return float(np.mean([a.brain.shell_coherence() for a in self.agents]))

    def mean_reward(self):
        return float(np.mean([a.last_reward for a in self.agents]))

    def mean_penalty(self):
        return float(np.mean([a.last_penalty for a in self.agents]))
