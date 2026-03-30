import numpy as np


def sync(phases) -> float:
    """Kuramoto order parameter over a phase array. Range [0, 1]."""
    return float(abs(np.mean(np.exp(1j * phases))))
