'use strict';

class CaveRenderer {
  constructor(canvas, scene) {
    this.canvas = canvas;
    this.ctx    = canvas.getContext('2d');
    this.scene  = scene;
    this._layout();
  }

  _layout() {
    const { canvas } = this;
    this.cx    = canvas.width  * 0.5;
    this.cy    = canvas.height * 0.47;
    this.ringR = Math.min(canvas.width, canvas.height) * 0.35;
    this.diskR = Math.min(canvas.width, canvas.height) * 0.068;

    // Place spirits at equal angles around the ring (top = -π/2)
    const spirits = this.scene.spirits;
    spirits.forEach((spirit, i) => {
      const angle = (i / spirits.length) * Math.PI * 2 - Math.PI / 2;
      const wx = this.cx + Math.cos(angle) * this.ringR;
      const wy = this.cy + Math.sin(angle) * this.ringR;
      spirit.wallX   = wx; spirit.wallY   = wy;
      spirit.targetX = this.cx; spirit.targetY = this.cy;
      spirit.moveSpeed = this.ringR / 3;  // proportional to ring size
      if (!spirit.awakened) { spirit.x = wx; spirit.y = wy; }
    });
  }

  resize(w, h) {
    this.canvas.width  = w;
    this.canvas.height = h;
    this._layout();
  }

  draw(wallTime) {
    const ctx    = this.ctx;
    const scene  = this.scene;
    const w      = this.canvas.width;
    const h      = this.canvas.height;
    const { cx, cy, ringR, diskR } = this;

    const chakra = scene.sessionCtrl.chakraState;
    const pulse  = scene.visualPulse.getPulseIntensity(chakra);
    const [cr, cg, cb] = scene.visualPulse.getChakraColor(chakra.activeBand);

    // ── Background ────────────────────────────────────────────────────────────
    ctx.fillStyle = '#08050302';
    ctx.fillRect(0, 0, w, h);

    const amb = ctx.createRadialGradient(cx, cy, 0, cx, cy, ringR * 2.5);
    amb.addColorStop(0,   'rgba(58, 32, 10, 0.65)');
    amb.addColorStop(0.55,'rgba(28, 14,  4, 0.45)');
    amb.addColorStop(1,   'rgba(0,   0,  0, 0)');
    ctx.fillStyle = amb;
    ctx.fillRect(0, 0, w, h);

    // ── Cave wall striations ───────────────────────────────────────────────────
    ctx.save();
    for (let r = ringR * 1.14; r < Math.max(w, h) * 0.9; r += 26) {
      const alpha = 0.07 * Math.max(0, 1 - (r - ringR * 1.14) / (ringR * 1.8));
      ctx.strokeStyle = `rgba(135, 98, 52, ${alpha})`;
      ctx.lineWidth   = 1;
      ctx.beginPath();
      ctx.arc(cx, cy, r, 0, Math.PI * 2);
      ctx.stroke();
    }
    ctx.restore();

    // ── Chakra ring ambient glow ──────────────────────────────────────────────
    const ringGlow = ctx.createRadialGradient(cx, cy, ringR * 0.82, cx, cy, ringR * 1.18);
    ringGlow.addColorStop(0,   `rgba(${cr},${cg},${cb}, 0)`);
    ringGlow.addColorStop(0.5, `rgba(${cr},${cg},${cb}, ${0.16 * pulse})`);
    ringGlow.addColorStop(1,   `rgba(${cr},${cg},${cb}, 0)`);
    ctx.fillStyle = ringGlow;
    ctx.beginPath();
    ctx.arc(cx, cy, ringR * 1.18, 0, Math.PI * 2);
    ctx.fill();

    // ── Sculpture ring dashed outline ─────────────────────────────────────────
    ctx.save();
    ctx.setLineDash([7, 11]);
    ctx.strokeStyle = `rgba(${cr},${cg},${cb}, ${0.22 * pulse})`;
    ctx.lineWidth   = 1.5;
    ctx.beginPath();
    ctx.arc(cx, cy, ringR, 0, Math.PI * 2);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.restore();

    // ── Spirits ───────────────────────────────────────────────────────────────
    const labels = ['Seated', 'Flow', 'Dervish', 'PairA', 'PairB'];
    scene.spirits.forEach((spirit, i) => {
      this._drawSpirit(spirit, labels[i], [cr, cg, cb], pulse, wallTime);
    });

    // ── Center disk ───────────────────────────────────────────────────────────
    this._drawCenterDisk([cr, cg, cb], pulse, wallTime);

    // ── Portal (placed between spirit 4 and 0, outer edge of ring) ───────────
    const portalAngle = -Math.PI / 2 + (0.5 / 5) * Math.PI * 2;
    const px = cx + Math.cos(portalAngle) * ringR * 1.22;
    const py = cy + Math.sin(portalAngle) * ringR * 1.22;
    this._drawPortal(px, py, scene.portalCtrl.portalUnlocked, [cr, cg, cb], wallTime);

    // ── Planet sigil (revealed on session complete) ───────────────────────────
    if (scene.symbolicOutcome.resultShown) {
      this._drawSigil(scene.symbolicOutcome.revealedPlanetId, wallTime);
    }

    // ── Session progress bar ──────────────────────────────────────────────────
    this._drawProgressBar(scene.sessionCtrl, [cr, cg, cb], w, h);
  }

  // ── Center Disk ─────────────────────────────────────────────────────────────
  _drawCenterDisk([r, g, b], pulse, t) {
    const ctx = this.ctx;
    const { cx, cy, diskR } = this;
    const fl = 1 + 0.045 * Math.sin(t * 2.8);

    // Wide outer glow
    const glow = ctx.createRadialGradient(cx, cy, 0, cx, cy, diskR * 4.2);
    glow.addColorStop(0,    `rgba(${r},${g},${b}, ${0.28 * pulse * fl})`);
    glow.addColorStop(0.32, `rgba(${r},${g},${b}, ${0.11 * pulse})`);
    glow.addColorStop(1,    `rgba(${r},${g},${b}, 0)`);
    ctx.fillStyle = glow;
    ctx.beginPath();
    ctx.arc(cx, cy, diskR * 4.2, 0, Math.PI * 2);
    ctx.fill();

    // Disk fill
    ctx.beginPath();
    ctx.arc(cx, cy, diskR, 0, Math.PI * 2);
    ctx.fillStyle   = `rgba(${r},${g},${b}, ${0.16 * pulse})`;
    ctx.fill();
    ctx.strokeStyle = `rgba(${r},${g},${b}, ${0.88 * pulse * fl})`;
    ctx.lineWidth   = 2.5;
    ctx.stroke();

    // Inner cross symbol
    ctx.save();
    ctx.strokeStyle = `rgba(${r},${g},${b}, ${0.48 * pulse})`;
    ctx.lineWidth   = 1;
    const arm = diskR * 0.52;
    ctx.beginPath();
    ctx.moveTo(cx - arm, cy); ctx.lineTo(cx + arm, cy);
    ctx.moveTo(cx, cy - arm); ctx.lineTo(cx, cy + arm);
    ctx.stroke();
    // Small center dot
    ctx.beginPath();
    ctx.arc(cx, cy, 3, 0, Math.PI * 2);
    ctx.fillStyle = `rgba(${r},${g},${b}, ${0.65 * pulse})`;
    ctx.fill();
    ctx.restore();
  }

  // ── Spirit Figure ─────────────────────────────────────────────────────────
  _drawSpirit(spirit, label, [r, g, b], pulse, t) {
    const ctx    = this.ctx;
    const active = spirit.awakened;
    const center = spirit.atCenter;
    const radius = active ? (center ? 13 + 3 * Math.sin(t * 4.8) : 11) : 8;
    const alpha  = active ? 0.92 : 0.33;
    const br     = active ? r : 165;
    const bg2    = active ? g : 125;
    const bb     = active ? b : 60;

    // Glow halo when awakened
    if (active) {
      const glow = ctx.createRadialGradient(spirit.x, spirit.y, 0, spirit.x, spirit.y, radius * 3.8);
      glow.addColorStop(0, `rgba(${r},${g},${b}, ${0.42 * pulse})`);
      glow.addColorStop(1, `rgba(${r},${g},${b}, 0)`);
      ctx.fillStyle = glow;
      ctx.beginPath();
      ctx.arc(spirit.x, spirit.y, radius * 3.8, 0, Math.PI * 2);
      ctx.fill();
    }

    // Body circle
    ctx.beginPath();
    ctx.arc(spirit.x, spirit.y, radius, 0, Math.PI * 2);
    ctx.fillStyle   = `rgba(${br},${bg2},${bb}, ${alpha})`;
    ctx.fill();
    ctx.strokeStyle = `rgba(238, 208, 138, ${alpha})`;
    ctx.lineWidth   = 1;
    ctx.stroke();

    // Small inner ring when at center
    if (center) {
      ctx.beginPath();
      ctx.arc(spirit.x, spirit.y, radius * 0.45, 0, Math.PI * 2);
      ctx.strokeStyle = `rgba(${r},${g},${b}, ${0.7 * pulse})`;
      ctx.lineWidth   = 1;
      ctx.stroke();
    }

    // Label
    ctx.fillStyle = `rgba(215, 178, 98, ${active ? 0.88 : 0.3})`;
    ctx.font      = '10px monospace';
    ctx.textAlign = 'center';
    ctx.fillText(label, spirit.x, spirit.y + radius + 13);
  }

  // ── Portal ─────────────────────────────────────────────────────────────────
  _drawPortal(px, py, unlocked, [r, g, b], t) {
    const ctx = this.ctx;
    const pw  = 18, ph = 32;

    if (unlocked) {
      const fl = 0.72 + 0.28 * Math.sin(t * 5.8);

      const glow = ctx.createRadialGradient(px, py, 0, px, py, pw * 4);
      glow.addColorStop(0, `rgba(${r},${g},${b}, ${0.52 * fl})`);
      glow.addColorStop(1, `rgba(${r},${g},${b}, 0)`);
      ctx.fillStyle = glow;
      ctx.fillRect(px - pw * 4, py - ph * 1.6, pw * 8, ph * 3.2);

      ctx.save();
      ctx.strokeStyle = `rgba(${r},${g},${b}, ${0.95 * fl})`;
      ctx.lineWidth   = 2.5;
      ctx.beginPath();
      ctx.moveTo(px - pw, py + ph / 2);
      ctx.lineTo(px - pw, py - ph / 2);
      ctx.arc(px, py - ph / 2, pw, Math.PI, 0);
      ctx.lineTo(px + pw, py + ph / 2);
      ctx.closePath();
      ctx.fillStyle = `rgba(${r},${g},${b}, ${0.11 * fl})`;
      ctx.fill();
      ctx.stroke();
      ctx.restore();

      ctx.fillStyle = `rgba(${r},${g},${b}, ${0.9 * fl})`;
      ctx.font      = '9px monospace';
      ctx.textAlign = 'center';
      ctx.fillText('Portal Open', px, py + ph / 2 + 14);
    } else {
      ctx.save();
      ctx.strokeStyle = 'rgba(98, 72, 40, 0.38)';
      ctx.lineWidth   = 1.5;
      ctx.beginPath();
      ctx.moveTo(px - pw, py + ph / 2);
      ctx.lineTo(px - pw, py - ph / 2);
      ctx.arc(px, py - ph / 2, pw, Math.PI, 0);
      ctx.lineTo(px + pw, py + ph / 2);
      ctx.closePath();
      ctx.stroke();
      ctx.restore();

      ctx.fillStyle = 'rgba(98, 72, 40, 0.38)';
      ctx.font      = '9px monospace';
      ctx.textAlign = 'center';
      ctx.fillText('Portal', px, py + ph / 2 + 13);
    }
  }

  // ── Planet Sigil ──────────────────────────────────────────────────────────
  _drawSigil(planetId, t) {
    const ctx = this.ctx;
    const { cx, cy, diskR } = this;
    const sx = cx;
    const sy = cy + diskR * 2.8;
    const alpha = 0.62 + 0.38 * Math.sin(t * 1.9);

    const planetColors = {
      ForestHeart:  [ 78, 198,  78],
      SkySpiral:    [ 88, 168, 255],
      SourceVeil:   [198, 138, 255],
      WaterFlow:    [ 38, 198, 218],
      MachineOrder: [198, 198,  88],
      DarkContrast: [138,  38, 198],
    };
    const [r, g, b] = planetColors[planetId] || [255, 255, 255];

    // Background glow
    const glow = ctx.createRadialGradient(sx, sy, 0, sx, sy, 62);
    glow.addColorStop(0, `rgba(${r},${g},${b}, ${0.38 * alpha})`);
    glow.addColorStop(1, `rgba(${r},${g},${b}, 0)`);
    ctx.fillStyle = glow;
    ctx.beginPath();
    ctx.arc(sx, sy, 62, 0, Math.PI * 2);
    ctx.fill();

    // Rotating double triangle (Star of David / hexagram style)
    ctx.save();
    ctx.strokeStyle = `rgba(${r},${g},${b}, ${alpha})`;
    ctx.lineWidth   = 2;
    for (let tri = 0; tri < 2; tri++) {
      ctx.beginPath();
      for (let v = 0; v < 3; v++) {
        const a  = (v / 3) * Math.PI * 2 + (tri * Math.PI / 3) + t * 0.38;
        const vx = sx + Math.cos(a) * 22;
        const vy = sy + Math.sin(a) * 22;
        v === 0 ? ctx.moveTo(vx, vy) : ctx.lineTo(vx, vy);
      }
      ctx.closePath();
      ctx.stroke();
    }
    // Centre dot
    ctx.beginPath();
    ctx.arc(sx, sy, 4, 0, Math.PI * 2);
    ctx.fillStyle = `rgba(${r},${g},${b}, ${alpha})`;
    ctx.fill();
    ctx.restore();

    // Planet name label
    ctx.fillStyle = `rgba(${r},${g},${b}, ${alpha})`;
    ctx.font      = 'bold 13px monospace';
    ctx.textAlign = 'center';
    ctx.fillText(planetId, sx, sy + 42);
  }

  // ── Session Progress Bar ──────────────────────────────────────────────────
  _drawProgressBar(sessionCtrl, [r, g, b], w, h) {
    const ctx      = this.ctx;
    const bx = 18, by = h - 20, bw = w - 36, bh = 5;
    const progress = Math.min(1, sessionCtrl.sessionTimer / sessionCtrl.totalSessionLength);

    // Track
    ctx.fillStyle = 'rgba(38, 22, 8, 0.82)';
    ctx.fillRect(bx, by, bw, bh);

    // Fill (chakra-colored)
    ctx.fillStyle = `rgb(${r},${g},${b})`;
    ctx.fillRect(bx, by, bw * progress, bh);

    // Crown hold start marker
    const holdFrac = sessionCtrl.crownHoldStart / sessionCtrl.totalSessionLength;
    ctx.fillStyle = 'rgba(255,255,255,0.32)';
    ctx.fillRect(bx + bw * holdFrac - 1, by - 4, 2, bh + 8);

    // Time label (top-right of bar)
    const fmtT = s => `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, '0')}`;
    ctx.fillStyle = 'rgba(195,155,75,0.58)';
    ctx.font      = '10px monospace';
    ctx.textAlign = 'right';
    ctx.fillText(
      `${fmtT(sessionCtrl.sessionTimer)} / ${fmtT(sessionCtrl.totalSessionLength)}`,
      w - 20, by - 6
    );
  }
}
