def apply_world_effect(agent, planet) -> None:
    """
    High-energy or high-sync agents leave a small imprint on their region.

    - High energy  → amplifies regional energy (positive feedback)
    - High sync    → dampens regional phase drift (stabilising feedback)
    """
    rid = agent.region_id

    if agent.energy > 2.0:
        planet.region_energies[rid] *= 1.02

    if agent.brain.sync() > 0.6:
        planet.region_phases[rid] *= 0.98
