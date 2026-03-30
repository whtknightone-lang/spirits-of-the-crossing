def choose_action(agent, coherence: float, mismatch: float, energy: float) -> str:
    """
    Map agent state to a behavioural action tag.

    Returns one of: EMIT, STABILIZE, MOVE, REST
    """
    if energy > 2.0:
        return "EMIT"
    if coherence > 0.6:
        return "STABILIZE"
    if mismatch > 0.4:
        return "MOVE"
    return "REST"
