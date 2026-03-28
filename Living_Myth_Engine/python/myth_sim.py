# Living myth simulation prototype

myths = {}

def register_myth(name, influence):
    myths[name] = influence

def evaluate_world():
    score = 0
    for m in myths.values():
        score += m
    return score

if __name__ == "__main__":
    register_myth("forest", 1.2)
    register_myth("machine", -0.5)
    print("World influence:", evaluate_world())
