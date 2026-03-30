import numpy as np


def resolve_encounter(a, b) -> str:
    """
    Resolve a pairwise meeting between two agents based on phase resonance.

    Returns one of: ALLY, ORBIT, REPEL
    """
    phase_diff = a.signature() - b.signature()
    resonance = np.cos(phase_diff)

    if resonance > 0.7:
        a.energy += 0.05
        b.energy += 0.05
        return "ALLY"
    elif resonance > 0.3:
        return "ORBIT"
    else:
        a.energy -= 0.03
        b.energy -= 0.03
        return "REPEL"
