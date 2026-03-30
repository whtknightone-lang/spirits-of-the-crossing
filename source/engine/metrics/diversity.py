import numpy as np


def diversity(phases) -> float:
    """Standard deviation of a phase array — higher means more spread."""
    return float(np.std(phases))
