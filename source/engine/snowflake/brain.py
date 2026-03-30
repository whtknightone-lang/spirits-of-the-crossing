import numpy as np


class SnowflakeBrain3DFluid:
    """
    3-layer spherical oscillator brain.

    Layers:
      center  (node 0)      — identity anchor
      inner   (nodes 1-6)   — octahedral shell, structural coherence
      outer   (nodes 7-20)  — fibonacci sphere (upsilon layer), fast-reacting

    Extras:
      shell       — fluid ring of 14 oscillators coupled to the outer layer
      core_energy — scalar reinforcement accumulator
      memory      — slow-drift phase trace for identity matching
    """

    def __init__(self):
        self.n = 21
        self.phases = np.random.rand(self.n) * 2 * np.pi
        self.positions = self._build_positions()
        self.matrix = self._build_matrix()

        self.memory = self.phases.copy()

        self.center = np.array([0])
        self.inner = np.arange(1, 7)
        self.outer = np.arange(7, 21)

        # fluid shell coupled to outer layer
        self.shell = np.random.rand(len(self.outer)) * 2 * np.pi

        # energy core
        self.core_energy = 1.0

        # Hebbian learning — base weights are frozen; matrix drifts around them
        self.base_matrix = self.matrix.copy()
        self.HEBBIAN_LR = 0.0005

    def _build_positions(self):
        pos = np.zeros((21, 3), dtype=float)
        pos[0] = np.array([0.0, 0.0, 0.0])

        inner = np.array([
            [ 1.0,  0.0,  0.0],
            [-1.0,  0.0,  0.0],
            [ 0.0,  1.0,  0.0],
            [ 0.0, -1.0,  0.0],
            [ 0.0,  0.0,  1.0],
            [ 0.0,  0.0, -1.0],
        ])
        pos[1:7] = inner * 0.75

        n_outer = 14
        ga = np.pi * (3.0 - np.sqrt(5.0))
        pts = []
        for i in range(n_outer):
            y = 1 - (i / float(n_outer - 1)) * 2
            radius = np.sqrt(max(0.0, 1 - y * y))
            theta = ga * i
            x = np.cos(theta) * radius
            z = np.sin(theta) * radius
            pts.append([x, y, z])
        pos[7:21] = np.array(pts) * 1.6
        return pos

    def _build_matrix(self):
        K = np.zeros((21, 21), dtype=float)

        def connect(a, b, w):
            K[a, b] = w
            K[b, a] = w

        # center <-> inner
        for i in range(1, 7):
            connect(0, i, 1.0)

        # inner ring cycle
        inner = [1, 2, 3, 4, 5, 6]
        for i in range(len(inner)):
            connect(inner[i], inner[(i + 1) % len(inner)], 0.65)

        # inner -> outer assignment (each inner node owns 2 outer nodes)
        mapping = {
            1: [7, 8],
            2: [9, 10],
            3: [11, 12],
            4: [13, 14],
            5: [15, 16],
            6: [17, 18],
        }
        for src, dsts in mapping.items():
            for d in dsts:
                connect(src, d, 0.50)

        # outer ring cycle
        outer = list(range(7, 21))
        for i in range(len(outer)):
            connect(outer[i], outer[(i + 1) % len(outer)], 0.35)

        # asymmetry bridges
        connect(1, 19, 0.45)
        connect(3, 20, 0.45)
        return K

    # ------------------------------------------------------------------
    # Observables
    # ------------------------------------------------------------------

    def sync(self, phases=None):
        """Kuramoto order parameter (0 = incoherent, 1 = locked)."""
        p = self.phases if phases is None else phases
        return float(abs(np.mean(np.exp(1j * p))))

    def mean_phase(self, phases=None):
        p = self.phases if phases is None else phases
        return float(np.angle(np.mean(np.exp(1j * p))))

    def identity_match(self, phases=None):
        """Cosine similarity between current phases and slow memory."""
        p = self.phases if phases is None else phases
        return float(np.mean(np.cos(p - self.memory)))

    def shell_coherence(self):
        return float(abs(np.mean(np.exp(1j * self.shell))))

    # ------------------------------------------------------------------
    # Dynamics
    # ------------------------------------------------------------------

    def simulate_one_step(self, drive=0.0, reinforcement=0.0):
        phases = self.phases.copy()
        shell = self.shell.copy()
        core_energy = float(self.core_energy)

        # Kuramoto-style coupling weighted by distance
        diff = phases[:, None] - phases[None, :]
        dist = np.linalg.norm(
            self.positions[:, None, :] - self.positions[None, :, :], axis=2
        )
        coupling = np.sin(diff) * self.matrix / (1.0 + dist)
        velocity = coupling.sum(axis=1)

        # fluid shell — ring diffusion
        shell_left = np.roll(shell, 1)
        shell_right = np.roll(shell, -1)
        shell_velocity = 0.08 * (np.sin(shell_left - shell) + np.sin(shell_right - shell))

        # bidirectional shell <-> outer coupling
        outer_phases = phases[self.outer]
        shell_velocity += 0.10 * np.sin(outer_phases - shell)
        velocity[self.outer] += 0.12 * np.sin(shell - outer_phases)

        # layer-specific pulls
        velocity[self.center] += (np.mean(phases[self.inner]) - phases[self.center]) * 0.01
        velocity[self.inner] += (np.mean(phases[self.outer]) - phases[self.inner]) * 0.004

        # upsilon outer shell reacts faster
        velocity[self.outer] *= 1.35

        # energy core drives center + inner
        core_push = 0.01 * np.tanh(core_energy) + 0.01 * reinforcement
        velocity[self.center] += core_push
        velocity[self.inner] += 0.5 * core_push

        velocity *= 0.04
        velocity += drive
        velocity = np.clip(velocity, -0.18, 0.18)
        shell_velocity = np.clip(shell_velocity, -0.12, 0.12)

        phases = np.mod(phases + velocity, 2 * np.pi)
        shell = np.mod(shell + shell_velocity, 2 * np.pi)

        # update core energy
        coherence = float(abs(np.mean(np.exp(1j * phases[self.inner]))))
        shell_coh = float(abs(np.mean(np.exp(1j * shell))))
        core_energy = (
            0.985 * core_energy
            + 0.03 * reinforcement
            + 0.01 * coherence
            + 0.01 * shell_coh
        )
        core_energy = float(np.clip(core_energy, 0.0, 3.0))

        return phases, shell, core_energy

    def step(self, drive=0.0, reinforcement=0.0):
        self.phases, self.shell, self.core_energy = self.simulate_one_step(
            drive=drive, reinforcement=reinforcement
        )
        # slow memory drift
        self.memory = 0.98 * self.memory + 0.02 * self.phases
        # Hebbian weight update
        self._hebbian_update(reinforcement)

    def _hebbian_update(self, reinforcement: float) -> None:
        """
        Oja-style Hebbian learning on the coupling matrix.

        When reinforcement is high, connections between co-active node pairs
        (those whose phases are correlated) are strengthened.
        A slow decay term keeps weights from drifting too far from base.

        Only existing connections (base_matrix > 0) are modified.
        Weights are clipped to [base * 0.3, base * 2.5].
        """
        if reinforcement < 1e-4:
            # still apply the decay even with zero reinforcement
            mask = self.base_matrix > 0
            decay = -0.0001 * (self.matrix - self.base_matrix) * mask
            self.matrix += decay
            return

        mask = self.base_matrix > 0
        phase_corr = np.cos(self.phases[:, None] - self.phases[None, :])
        dK = self.HEBBIAN_LR * reinforcement * phase_corr * mask
        decay = -0.0001 * (self.matrix - self.base_matrix) * mask
        self.matrix = np.clip(
            self.matrix + dK + decay,
            self.base_matrix * 0.3,
            self.base_matrix * 2.5,
        )
