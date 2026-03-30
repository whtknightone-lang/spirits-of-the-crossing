import numpy as np


def mean_energy(energies) -> float:
    """Mean agent energy across the population."""
    return float(np.mean(energies))
