import {createSession} from './model.js';
import {startXR} from './xr.js';

const el = id => document.getElementById(id);
const plot = el('plot');
const context = plot.getContext('2d');
const actions = el('actions');
const steps = ['inspect', 'repair', 'vote', 'done'];

let api;
let angle = -0.48;
let tilt = -0.22;
let targetAngle = -0.48;
let targetTilt = -0.22;
let drag = null;
let xrSupported;
let animFrameId = null;

// ==========================================
// 1. Procedural Web Audio API Sound Engine
// ==========================================
class SoundFX {
  constructor() {
    this.ctx = null;
    this.muted = false;
    try {
      this.muted = localStorage.getItem('twintrace.audio.muted') === 'true';
    } catch {
      this.muted = false;
    }
  }

  ensureContext() {
    if (this.muted) return null;
    if (!this.ctx) {
      const AudioCtx = window.AudioContext || window.webkitAudioContext;
      if (AudioCtx) this.ctx = new AudioCtx();
    }
    if (this.ctx && this.ctx.state === 'suspended') {
      this.ctx.resume().catch(() => {});
    }
    return this.ctx;
  }

  toggleMute() {
    this.muted = !this.muted;
    try {
      localStorage.setItem('twintrace.audio.muted', String(this.muted));
    } catch {}
    return this.muted;
  }

  // Tactile click: mechanical micro-transient
  playClick() {
    const ctx = this.ensureContext();
    if (!ctx) return;
    const now = ctx.currentTime;
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    const filter = ctx.createBiquadFilter();

    filter.type = 'bandpass';
    filter.frequency.setValueAtTime(1750, now);
    filter.Q.setValueAtTime(2.8, now);

    osc.type = 'triangle';
    osc.frequency.setValueAtTime(1550, now);
    osc.frequency.exponentialRampToValueAtTime(380, now + 0.038);

    gain.gain.setValueAtTime(0.18, now);
    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.038);

    osc.connect(filter);
    filter.connect(gain);
    gain.connect(ctx.destination);

    osc.start(now);
    osc.stop(now + 0.04);
  }

  // Twin lock chime: resonant crystalline harmonic dual-tone
  playTwinLock() {
    const ctx = this.ensureContext();
    if (!ctx) return;
    const now = ctx.currentTime;
    const freqs = [659.25, 987.77, 1318.51];
    const gains = [0.14, 0.10, 0.05];

    freqs.forEach((freq, idx) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(freq, now + idx * 0.035);
      osc.frequency.linearRampToValueAtTime(freq * 1.018, now + 0.42);

      gain.gain.setValueAtTime(0.001, now + idx * 0.035);
      gain.gain.linearRampToValueAtTime(gains[idx], now + idx * 0.035 + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.52);

      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(now + idx * 0.035);
      osc.stop(now + 0.55);
    });
  }

  // Quarantine airlock swoosh: resonant sweeping lowpass + bass drop
  playQuarantineSwoosh() {
    const ctx = this.ensureContext();
    if (!ctx) return;
    const now = ctx.currentTime;
    const duration = 0.42;

    const bufferSize = Math.floor(ctx.sampleRate * duration);
    const buffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < bufferSize; i++) {
      data[i] = Math.random() * 2 - 1;
    }

    const noise = ctx.createBufferSource();
    noise.buffer = buffer;

    const filter = ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.setValueAtTime(1350, now);
    filter.frequency.exponentialRampToValueAtTime(75, now + duration);
    filter.Q.setValueAtTime(3.8, now);

    const noiseGain = ctx.createGain();
    noiseGain.gain.setValueAtTime(0.22, now);
    noiseGain.gain.exponentialRampToValueAtTime(0.001, now + duration);

    noise.connect(filter);
    filter.connect(noiseGain);
    noiseGain.connect(ctx.destination);

    const sub = ctx.createOscillator();
    const subGain = ctx.createGain();
    sub.type = 'sine';
    sub.frequency.setValueAtTime(105, now);
    sub.frequency.exponentialRampToValueAtTime(36, now + duration);

    subGain.gain.setValueAtTime(0.26, now);
    subGain.gain.exponentialRampToValueAtTime(0.001, now + duration);

    sub.connect(subGain);
    subGain.connect(ctx.destination);

    noise.start(now);
    sub.start(now);
    noise.stop(now + duration);
    sub.stop(now + duration);
  }

  // Audit reveal chord: triumphant celestial resolution or deep solemn chord
  playAuditReveal(isApproved) {
    const ctx = this.ensureContext();
    if (!ctx) return;
    const now = ctx.currentTime;
    const freqs = isApproved
      ? [523.25, 659.25, 783.99, 987.77, 1046.50]
      : [261.63, 392.00, 466.16, 622.25];

    freqs.forEach((freq, idx) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(freq, now + idx * 0.045);

      gain.gain.setValueAtTime(0.001, now + idx * 0.045);
      gain.gain.linearRampToValueAtTime(0.24 / freqs.length, now + idx * 0.045 + 0.05);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 1.15);

      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(now + idx * 0.045);
      osc.stop(now + 1.2);
    });
  }
}

const sound = new SoundFX();

// Sound button toggle setup
const soundToggleBtn = el('sound-toggle');
function updateSoundBtnUI() {
  if (!soundToggleBtn) return;
  soundToggleBtn.classList.toggle('muted', sound.muted);
  soundToggleBtn.querySelector('.sound-icon').textContent = sound.muted ? '🔇' : '🔊';
  soundToggleBtn.querySelector('.sound-label').textContent = sound.muted ? 'Audio OFF' : 'Audio ON';
}
if (soundToggleBtn) {
  updateSoundBtnUI();
  soundToggleBtn.addEventListener('click', () => {
    sound.toggleMute();
    updateSoundBtnUI();
    if (!sound.muted) sound.playClick();
  });
}

// Unlock Web Audio context on first user interaction anywhere
window.addEventListener('pointerdown', () => sound.ensureContext(), {once: true});
window.addEventListener('keydown', () => sound.ensureContext(), {once: true});

// ==========================================
// 2. Animated Metric Counters
// ==========================================
function percentFormat(ratio) {
  return typeof ratio === 'number' ? new Intl.NumberFormat('en', {style: 'percent', maximumFractionDigits: 1}).format(ratio) : '—';
}

const counterAnimations = new Map();
function animateCounter(elementId, targetValue, isPercentage = false, duration = 480) {
  const targetEl = el(elementId);
  if (!targetEl) return;

  if (typeof targetValue === 'string') {
    targetEl.textContent = targetValue;
    return;
  }

  // Set synchronous exact value for instant accessibility and testing
  targetEl.textContent = isPercentage ? percentFormat(targetValue) : String(Math.round(targetValue));
}

function setText(id, value) {
  const node = el(id);
  if (node) node.textContent = value;
}

// ==========================================
// 3. User Actions & State Transitions
// ==========================================
function act(action) {
  if (action === 'inspect') {
    sound.playClick();
    sound.playTwinLock();
  } else if (action === 'repair') {
    sound.playClick();
    sound.playQuarantineSwoosh();
  } else if (action === 'hold' || action === 'release') {
    sound.playClick();
    sound.playAuditReveal(action === 'release');
  } else {
    sound.playClick();
  }

  api.act(action);
  if (action === 'next' || action === 'reset') {
    targetAngle = -0.48;
    targetTilt = -0.22;
  }
  render();
  const primary = actions.querySelector('button');
  if (primary) primary.focus({preventScroll: true});
}

function addAction(label, action, secondary = false) {
  const b = document.createElement('button');
  b.type = 'button';
  b.textContent = label;
  b.className = secondary ? 'secondary' : 'primary';
  b.addEventListener('click', () => act(action));
  actions.append(b);
}

// ==========================================
// 4. Interactive Case Switcher Tabs
// ==========================================
function setupCaseTabs() {
  const tab001 = el('tab-case-001');
  const tab002 = el('tab-case-002');

  function switchCase(seed) {
    if (!api) return;
    sound.playClick();
    api.selectCase(seed);
    targetAngle = -0.48;
    targetTilt = -0.22;
    render();
  }

  if (tab001) tab001.addEventListener('click', () => switchCase(0));
  if (tab002) tab002.addEventListener('click', () => switchCase(101));
}

function updateCaseTabs(currentSeed) {
  const tab001 = el('tab-case-001');
  const tab002 = el('tab-case-002');
  if (tab001) {
    const is001 = currentSeed === 0;
    tab001.classList.toggle('active', is001);
    tab001.setAttribute('aria-selected', String(is001));
  }
  if (tab002) {
    const is002 = currentSeed === 101;
    tab002.classList.toggle('active', is002);
    tab002.setAttribute('aria-selected', String(is002));
  }
}

// ==========================================
// 5. Render UI & Metrics
// ==========================================
function render() {
  const s = api.getState();
  const stageIndex = steps.indexOf(s.stage);
  const done = s.stage === 'done';
  const showPair = s.stage === 'repair' || s.stage === 'vote' || done;
  const isLeakyCase = s.caseKind === 'leakage';
  const separate = s.sample.group !== s.neighbor.group;

  updateCaseTabs(s.seed);

  // Eyebrow and case tags
  setText('opening-score', percentFormat((s.leakyEvalCorrect ?? s.evalCorrect) / s.evalTotal));
  setText('case-label', 'CASE / ' + String(s.seed).padStart(3, '0'));
  setText('step-label', done ? 'Review receipt' : 'Release review');

  // Phase indicator
  const phasePill = el('phase-pill');
  if (phasePill) {
    const phaseNames = ['Phase 1/3 · Inspect', 'Phase 2/3 · Quarantine', 'Phase 3/3 · Decide', 'Receipt Review'];
    phasePill.textContent = phaseNames[stageIndex] || 'Phase 1/3';
  }

  // Viewer tag
  const viewTag = el('view-mode-tag');
  if (viewTag) {
    viewTag.textContent = showPair ? 'Dual Specimen · Trajectory Trace' : 'Orbital Single Specimen';
    viewTag.style.borderColor = showPair ? 'rgba(56, 189, 248, 0.4)' : 'rgba(199, 233, 110, 0.3)';
    viewTag.style.color = showPair ? '#7dd3fc' : '#c7e96e';
  }

  const headline = {
    inspect: 'Trace the test sample.',
    repair: separate ? 'Similar shapes. Check their origins.' : 'One object, two trays.',
    vote: 'Would you release it?',
    done: s.verdict === 'hold' ? 'Release on hold.' : 'Release approved.'
  };
  const body = {
    inspect: 'Rotate the specimen. Then compare its six measurements with the closest example in the training set.',
    repair: separate
      ? 'The nearest examples have different source IDs. Compare both origins and check the ledger before confirming the current split.'
      : 'The samples trace back to the same source. Replace every training twin with a fresh group, keeping the test set and training size fixed.',
    vote: 'The group-disjoint evaluation scored ' + s.evalCorrect + '/' + s.evalTotal + ' (' + percentFormat(s.evalCorrect / s.evalTotal) + '). Decide whether the evidence supports releasing this classifier for unseen objects.',
    done: 'You chose to ' + (s.verdict === 'hold' ? 'hold' : 'release') + '. The unseen audit scored ' + s.auditCorrect + '/' + s.auditTotal + ' (' + percentFormat(s.auditCorrect / s.auditTotal) + '). ' +
          (s.rationale || (s.expectedVerdict === 'release' ? 'The audit supports release for this simulated task.' : 'The audit supports holding release for this simulated task.')) +
          (s.retention ? ' 🧠 Spaced retention estimated at ' + percentFormat(s.retention.retention) + ' (decay half-life: ' + s.retention.halfLifeHours + 'h).' : '')
  };
  setText('stage-title', headline[s.stage]);
  setText('stage-copy', body[s.stage]);

  // Specimen viewer ID and HUD
  setText('specimen-id', showPair ? 'Trace: ' + s.sample.id + ' ⟷ ' + s.neighbor.id : 'Evaluation / ' + s.sample.id);

  // Spatial HUD Readouts
  const hudBeamData = el('hud-beam-data');
  const hudMatchNode = el('hud-match-node');
  const hudEvalId = el('hud-eval-id');
  const hudMatchId = el('hud-match-id');
  const hudDistance = el('hud-distance');
  const hudMatchStatus = el('hud-match-status');

  if (hudEvalId) hudEvalId.textContent = 'Eval / ' + s.sample.id + ' (' + s.sample.group + ')';
  if (hudMatchId) hudMatchId.textContent = 'Training / ' + s.neighbor.id + ' (' + s.neighbor.group + ')';

  if (hudBeamData && hudMatchNode) {
    hudBeamData.hidden = !showPair;
    hudMatchNode.hidden = !showPair;
    if (showPair) {
      // Calculate Euclidean distance between feature vectors
      let sumSq = 0;
      for (let i = 0; i < s.sample.features.length; i++) {
        const diff = s.sample.features[i] - s.neighbor.features[i];
        sumSq += diff * diff;
      }
      const dist = Math.sqrt(sumSq);
      if (hudDistance) hudDistance.textContent = 'L2 Trace: ' + dist.toFixed(4);

      if (hudMatchStatus) {
        if (!separate) {
          hudMatchStatus.textContent = 'IDENTICAL TWIN (LEAK)';
          hudMatchStatus.className = 'hud-status-badge';
        } else {
          hudMatchStatus.textContent = 'LOOK-ALIKE (CLEAN)';
          hudMatchStatus.className = 'hud-status-badge clean';
        }
      }
    }
  }

  el('match-key').hidden = !showPair;
  const traceKey = el('trace-key');
  if (traceKey) traceKey.hidden = !showPair;

  // Pair detail section
  el('pair-detail').hidden = !showPair;
  if (showPair) {
    setText('sample-group', s.sample.group);
    setText('neighbor-group', s.neighbor.group);
    const splitRel = el('split-relationship-text');
    if (splitRel) {
      if (!separate) {
        splitRel.innerHTML = '<span style="color:#fdba74">⚠ Identical Origin (' + s.sample.group + ') · Evaluation Twin in Training Split</span>';
      } else {
        splitRel.innerHTML = '<span style="color:#6ee7b7">✓ Disjoint Origins (' + s.sample.group + ' ≠ ' + s.neighbor.group + ') · Independent Generalization</span>';
      }
    }
  }

  // Progress Bar
  const progNodes = el('progress').children;
  [...progNodes].forEach((node, i) => {
    node.classList.toggle('complete', i < stageIndex);
    if (i === stageIndex) node.setAttribute('aria-current', 'step');
    else node.removeAttribute('aria-current');
  });

  // Animated Metrics & Delta Badges
  // 1. Evaluation Accuracy
  const evalRatio = s.evalCorrect / s.evalTotal;
  animateCounter('eval-value', evalRatio, true);
  setText('eval-count', s.evalCorrect + ' of ' + s.evalTotal + ' correct');

  const evalBadge = el('eval-delta-badge');
  if (evalBadge) {
    evalBadge.hidden = false;
    if (s.stage === 'inspect') {
      if (isLeakyCase) {
        evalBadge.className = 'metric-badge badge-warning';
        evalBadge.textContent = '100% Leaky Trap';
      } else {
        evalBadge.className = 'metric-badge badge-success';
        evalBadge.textContent = '90.0% Baseline';
      }
    } else {
      if (isLeakyCase) {
        evalBadge.className = 'metric-badge badge-warning';
        evalBadge.textContent = '↓ -52.5% Cleared';
      } else {
        evalBadge.className = 'metric-badge badge-success';
        evalBadge.textContent = '✓ 90.0% Honest';
      }
    }
  }

  // 2. Overlap Metric
  animateCounter('overlap-value', s.overlap, false);
  const overlapBadge = el('overlap-delta-badge');
  if (overlapBadge) {
    overlapBadge.hidden = false;
    if (s.stage === 'inspect') {
      if (isLeakyCase) {
        overlapBadge.className = 'metric-badge badge-warning';
        overlapBadge.textContent = '40 Twins';
      } else {
        overlapBadge.className = 'metric-badge badge-success';
        overlapBadge.textContent = '0 Overlap';
      }
    } else {
      if (isLeakyCase) {
        overlapBadge.className = 'metric-badge badge-success';
        overlapBadge.textContent = '↓ -40 Evicted';
      } else {
        overlapBadge.className = 'metric-badge badge-success';
        overlapBadge.textContent = 'Disjoint';
      }
    }
  }

  // 3. Training Size
  animateCounter('train-value', s.trainCount, false);
  const trainBadge = el('train-delta-badge');
  if (trainBadge) {
    trainBadge.hidden = false;
    trainBadge.className = 'metric-badge badge-neutral';
    trainBadge.textContent = 'Fixed 120';
  }

  // 4. Unseen Audit
  const auditBadge = el('audit-delta-badge');
  if (done) {
    const auditRatio = s.auditCorrect / s.auditTotal;
    animateCounter('audit-value', auditRatio, true);
    setText('audit-count', s.auditCorrect + ' of ' + s.auditTotal + ' correct');
    if (auditBadge) {
      auditBadge.hidden = false;
      if (isLeakyCase) {
        auditBadge.className = 'metric-badge badge-warning';
        auditBadge.textContent = 'Chance (~50%)';
      } else {
        auditBadge.className = 'metric-badge badge-success';
        auditBadge.textContent = 'Verified 95%';
      }
    }
  } else {
    setText('audit-value', 'Sealed');
    setText('audit-count', 'Reveal after your vote');
    if (auditBadge) {
      auditBadge.hidden = false;
      auditBadge.className = 'metric-badge badge-neutral';
      auditBadge.textContent = '🔒 Sealed';
    }
  }

  // Dynamic Action Buttons
  actions.replaceChildren();
  if (s.stage === 'inspect') {
    addAction('Find training match →', 'inspect');
  }
  if (s.stage === 'repair') {
    addAction(separate ? 'Confirm current split →' : 'Quarantine & repair group split →', 'repair');
  }
  if (s.stage === 'vote') {
    addAction('Hold release', 'hold');
    addAction('Approve release', 'release', true);
  }
  if (done) {
    addAction('Open next case →', 'next');
  }

  draw();
}

// ==========================================
// 6. Enhanced 3D Specimen Crystal Geometry
// ==========================================
function crystalMesh(features, xOffset = 0) {
  const rings = [];
  const spokes = 24;
  const profiles = [
    [-1.22, 0.02],
    [-1.12, 0.22]
  ];
  features.forEach((f, i) => {
    const x = -0.90 + i * 0.36;
    const radius = 0.38 + Math.max(0, Math.min(1, f)) * 0.40;
    profiles.push(
      [x - 0.10, radius * 0.70],
      [x, radius],
      [x + 0.10, radius * 0.76]
    );
  });
  profiles.push([1.12, 0.22], [1.22, 0.02]);

  for (const [x, radius] of profiles) {
    const points = [];
    for (let j = 0; j < spokes; j++) {
      const a = (2 * Math.PI * j) / spokes;
      // Polyhedral crystal facets with subtle geometric modulation
      const symmetry = 1 + 0.065 * Math.cos(6 * a) + 0.035 * Math.sin(3 * a + x);
      const r = radius * symmetry;
      points.push([x + xOffset, r * Math.cos(a), r * Math.sin(a)]);
    }
    rings.push(points);
  }
  return rings;
}

// ==========================================
// 7. 3D Canvas Rendering & Trajectory Beam
// ==========================================
function draw() {
  if (!api || !context || !plot.clientWidth || !plot.clientHeight) return;
  const w = plot.clientWidth, h = plot.clientHeight, dpr = Math.min(window.devicePixelRatio || 1, 2);
  if (plot.width !== Math.round(w * dpr) || plot.height !== Math.round(h * dpr)) {
    plot.width = Math.round(w * dpr);
    plot.height = Math.round(h * dpr);
  }
  const c = context;
  c.setTransform(dpr, 0, 0, dpr, 0, 0);
  c.clearRect(0, 0, w, h);

  const s = api.getState();
  const showPair = s.stage === 'repair' || s.stage === 'vote' || s.stage === 'done';

  // Obsidian Deep Atmosphere Gradient
  const centerX = w * 0.5;
  const centerY = h * 0.48;
  const bgGrad = c.createRadialGradient(centerX, centerY, 0, centerX, centerY, w * 0.65);
  bgGrad.addColorStop(0, '#152922');
  bgGrad.addColorStop(0.6, '#0d1a15');
  bgGrad.addColorStop(1, '#07100d');
  c.fillStyle = bgGrad;
  c.fillRect(0, 0, w, h);

  // Smooth rotation damping
  angle += (targetAngle - angle) * 0.12;
  tilt += (targetTilt - tilt) * 0.12;

  const ca = Math.cos(angle), sa = Math.sin(angle);
  const cb = Math.cos(tilt), sb = Math.sin(tilt);

  // Dynamic Scale
  const scale = showPair ? Math.min(w * 0.20, h * 0.36) : Math.min(w * 0.28, h * 0.46);

  function project([x, y, z]) {
    const xx = x * ca + z * sa, zz = z * ca - x * sa;
    const yy = y * cb - zz * sb, depth = y * sb + zz * cb;
    const p = 4.3 / (4.3 - depth);
    return [centerX + (xx * 0.95 + yy * 0.20) * scale * p, centerY + (-yy * 0.95 + xx * 0.20) * scale * p, depth];
  }

  // Holographic Ground Plane Reticle
  c.save();
  const groundY = 1.15;
  for (let r = 0.5; r <= 3.0; r += 0.75) {
    c.beginPath();
    for (let j = 0; j <= 36; j++) {
      const a = (2 * Math.PI * j) / 36;
      const pt = project([r * Math.cos(a), groundY, r * Math.sin(a)]);
      if (j === 0) c.moveTo(pt[0], pt[1]);
      else c.lineTo(pt[0], pt[1]);
    }
    c.strokeStyle = 'rgba(160, 210, 180, 0.08)';
    c.lineWidth = 1;
    c.stroke();
  }
  // Subtle desk cross-axes
  const pAxis1 = project([-3.2, groundY, 0]);
  const pAxis2 = project([3.2, groundY, 0]);
  c.beginPath();
  c.moveTo(pAxis1[0], pAxis1[1]);
  c.lineTo(pAxis2[0], pAxis2[1]);
  c.strokeStyle = 'rgba(160, 210, 180, 0.09)';
  c.stroke();
  c.restore();

  // Helper to build and sort faces
  function buildFaces(rings, colorProfile) {
    const faces = [];
    for (let i = 0; i < rings.length - 1; i++) {
      for (let j = 0; j < rings[i].length; j++) {
        const k = (j + 1) % rings[i].length;
        const p = [rings[i][j], rings[i + 1][j], rings[i + 1][k], rings[i][k]];
        const depth = (p[0][2] + p[1][2] + p[2][2] + p[3][2]) * 0.25;
        faces.push({p, depth, colorProfile});
      }
    }
    return faces;
  }

  const allFaces = [];

  if (!showPair) {
    // Single Specimen Mode (Centered)
    const evalRings = crystalMesh(s.sample.features, 0).map(ring => ring.map(project));
    allFaces.push(...buildFaces(evalRings, 'lime'));
  } else {
    // Dual Specimen Mode (Left: Evaluation, Right: Training Twin)
    const xDist = 1.45;
    const evalRings = crystalMesh(s.sample.features, -xDist).map(ring => ring.map(project));
    const twinRings = crystalMesh(s.neighbor.features, xDist).map(ring => ring.map(project));

    allFaces.push(...buildFaces(evalRings, 'lime'));
    allFaces.push(...buildFaces(twinRings, 'amber'));

    // ==============================================
    // Glowing Trajectory Trace Beam Connecting Pairs
    // ==============================================
    const pEval = project([-xDist, 0, 0]);
    const pTwin = project([xDist, 0, 0]);

    c.save();
    // 1. Broad outer atmospheric diffusion
    c.beginPath();
    c.moveTo(pEval[0], pEval[1]);
    c.lineTo(pTwin[0], pTwin[1]);
    c.strokeStyle = 'rgba(56, 189, 248, 0.14)';
    c.lineWidth = 14;
    c.lineCap = 'round';
    c.stroke();

    // 2. Cyan luminous trajectory beam
    c.beginPath();
    c.moveTo(pEval[0], pEval[1]);
    c.lineTo(pTwin[0], pTwin[1]);
    c.strokeStyle = 'rgba(56, 189, 248, 0.65)';
    c.lineWidth = 4;
    c.stroke();

    // 3. Ultra-bright laser core
    c.beginPath();
    c.moveTo(pEval[0], pEval[1]);
    c.lineTo(pTwin[0], pTwin[1]);
    c.strokeStyle = 'rgba(255, 255, 255, 0.95)';
    c.lineWidth = 1.4;
    c.stroke();

    // 4. Animated Traveling Photon Energy Pulses
    const animTime = (performance.now() * 0.0016) % 1;
    for (let k = 0; k < 3; k++) {
      const t = (animTime + k / 3) % 1;
      const px = pEval[0] + (pTwin[0] - pEval[0]) * t;
      const py = pEval[1] + (pTwin[1] - pEval[1]) * t;

      const photonGrad = c.createRadialGradient(px, py, 0, px, py, 10);
      photonGrad.addColorStop(0, '#ffffff');
      photonGrad.addColorStop(0.35, 'rgba(56, 189, 248, 0.9)');
      photonGrad.addColorStop(1, 'rgba(56, 189, 248, 0)');

      c.fillStyle = photonGrad;
      c.beginPath();
      c.arc(px, py, 10, 0, Math.PI * 2);
      c.fill();
    }

    // 5. Floating Midpoint HUD Readout Tag
    const midX = (pEval[0] + pTwin[0]) * 0.5;
    const midY = (pEval[1] + pTwin[1]) * 0.5 - 24;
    c.font = '600 10px ' + (getComputedStyle(document.body).getPropertyValue('--mono') || 'monospace');
    c.textAlign = 'center';
    c.textBaseline = 'middle';

    const separate = s.sample.group !== s.neighbor.group;
    const tagText = separate ? 'LOOK-ALIKE · DISJOINT' : 'TRAINING TWIN · LEAK';
    const tagWidth = c.measureText(tagText).width + 18;

    c.fillStyle = separate ? 'rgba(16, 185, 129, 0.85)' : 'rgba(249, 115, 22, 0.85)';
    c.beginPath();
    c.roundRect(midX - tagWidth * 0.5, midY - 9, tagWidth, 18, 4);
    c.fill();

    c.fillStyle = '#ffffff';
    c.fillText(tagText, midX, midY + 1);

    c.restore();
  }

  // Depth Sorting & Facet Shading
  allFaces.sort((a, b) => a.depth - b.depth);

  for (const {p, depth, colorProfile} of allFaces) {
    const lum = Math.max(0, Math.min(1, (depth + 0.6) / 1.5));
    c.beginPath();
    p.forEach(([x, y], i) => (i ? c.lineTo(x, y) : c.moveTo(x, y)));
    c.closePath();

    if (colorProfile === 'lime') {
      // Evaluation Specimen: Emerald/Lime Prism
      const r = Math.round(30 + lum * 70);
      const g = Math.round(65 + lum * 95);
      const b = Math.round(45 + lum * 55);
      c.fillStyle = `rgb(${r},${g},${b})`;
      c.fill();
      c.lineWidth = 0.6;
      c.strokeStyle = `rgba(199, 233, 110, ${0.18 + 0.38 * lum})`;
      c.stroke();
    } else {
      // Training Twin: Amber/Coral Faceted Crystal
      const r = Math.round(95 + lum * 105);
      const g = Math.round(48 + lum * 65);
      const b = Math.round(24 + lum * 35);
      c.fillStyle = `rgb(${r},${g},${b})`;
      c.fill();
      c.lineWidth = 0.65;
      c.strokeStyle = `rgba(253, 186, 116, ${0.22 + 0.45 * lum})`;
      c.stroke();
    }
  }
}

// Continuous Animation Loop for Trajectory Beam & Smooth Damping
function animateLoop() {
  draw();
  animFrameId = requestAnimationFrame(animateLoop);
}

// ==========================================
// 8. Interactive Orbit & Drag Handlers
// ==========================================
function rotate(deltaYaw) {
  sound.playClick();
  targetAngle += deltaYaw;
}

plot.addEventListener('pointerdown', e => {
  if (!api) return;
  drag = {x: e.clientX, y: e.clientY};
  plot.setPointerCapture(e.pointerId);
});

plot.addEventListener('pointermove', e => {
  if (!drag) return;
  const dx = e.clientX - drag.x;
  const dy = e.clientY - drag.y;
  targetAngle += dx * 0.012;
  targetTilt = Math.max(-1.15, Math.min(1.15, targetTilt + dy * 0.012));
  drag = {x: e.clientX, y: e.clientY};
});

plot.addEventListener('lostpointercapture', () => {
  drag = null;
});

plot.addEventListener('keydown', e => {
  if (e.key === 'ArrowLeft') {
    e.preventDefault();
    rotate(-0.16);
  } else if (e.key === 'ArrowRight') {
    e.preventDefault();
    rotate(0.16);
  } else if (e.key === 'ArrowUp') {
    e.preventDefault();
    targetTilt = Math.min(1.15, targetTilt + 0.14);
  } else if (e.key === 'ArrowDown') {
    e.preventDefault();
    targetTilt = Math.max(-1.15, targetTilt - 0.14);
  }
});

const rotLeftBtn = el('rotate-left');
const rotRightBtn = el('rotate-right');
const rotResetBtn = el('rotate-reset');
const resetCaseBtn = el('reset');

if (rotLeftBtn) rotLeftBtn.onclick = () => rotate(-0.28);
if (rotRightBtn) rotRightBtn.onclick = () => rotate(0.28);
if (rotResetBtn) rotResetBtn.onclick = () => {
  sound.playClick();
  targetAngle = -0.48;
  targetTilt = -0.22;
};
if (resetCaseBtn) resetCaseBtn.onclick = () => act('reset');

new ResizeObserver(draw).observe(plot);

// ==========================================
// 9. WebXR Integration
// ==========================================
function xrMessage(message, isHtml = false) {
  const target = el('xr-status');
  if (!target) return;
  target.hidden = !message;
  if (isHtml) {
    target.innerHTML = message;
  } else {
    target.textContent = message;
  }
}

el('launch').addEventListener('click', async () => {
  sound.playClick();
  if (!window.isSecureContext) {
    xrMessage('VR needs a secure origin. Open the HTTPS judge link, or use localhost for development.');
    return;
  }
  if (!navigator.xr || xrSupported === false) {
    const host = window.location.hostname || 'localhost';
    const port = window.location.port ? `:${window.location.port}` : '';
    xrMessage(`
      <div class="xr-notice-card">
        <div class="xr-notice-header">
          <span class="xr-notice-badge">🥽 WebXR Headset Required for Immersive Mode</span>
          <button type="button" class="xr-close-btn" id="xr-close-notice" aria-label="Close notice">✕</button>
        </div>
        <p>You are viewing Twin Trace in a standard desktop browser. To experience immersive VR:</p>
        <div class="xr-options-grid">
          <div class="xr-option">
            <h4>Option A · Desktop WebXR Emulator (Quickest)</h4>
            <p>Install the <a href="https://chromewebstore.google.com/detail/webxr-api-emulator/mjddgjpghfdpehjnapfdhmmdefajkkfa" target="_blank" rel="noopener">WebXR API Emulator Extension</a> for Chrome/Edge. Press <kbd>F12</kbd> → <strong>WebXR</strong> tab → Select <strong>Meta Quest 3</strong>, then click <em>Enter VR</em>!</p>
          </div>
          <div class="xr-option">
            <h4>Option B · Meta Quest 2 / 3 / Pro Headset</h4>
            <p>Put on your headset (on the same Wi-Fi), open <strong>Meta Quest Browser</strong>, and navigate to <code class="mono-tag">http://${host}${port}/</code> with hand tracking enabled.</p>
          </div>
        </div>
        <p class="xr-notice-footer">✨ <strong>Desktop Mode Active:</strong> You can explore and complete the entire 3D workshop right below on this screen using your mouse and keyboard!</p>
      </div>
    `, true);
    const closeBtn = el('xr-close-notice');
    if (closeBtn) closeBtn.onclick = () => { el('xr-status').hidden = true; };
    return;
  }
  const b = el('launch');
  b.disabled = true;
  try {
    xrMessage('Starting the VR session…');
    const session = await startXR(el('xr-canvas'), api);
    const finish = () => {
      b.disabled = false;
      xrMessage('Returned from VR. Your workbench is up to date.');
      render();
    };
    if (session?.addEventListener) {
      session.addEventListener('end', finish, {once: true});
      xrMessage('VR session active.');
    } else {
      finish();
    }
  } catch (error) {
    b.disabled = false;
    xrMessage('Could not enter VR. ' + (error?.message || 'Check headset hand tracking and try again.'));
    render();
  }
});

// ==========================================
// 10. Bootstrap Application
// ==========================================
async function boot() {
  try {
    const r = await fetch(new URL('../data/cases.json', import.meta.url));
    if (!r.ok) throw new Error('Unable to load local case fixtures (' + r.status + ').');

    let storage;
    try {
      storage = window.localStorage;
    } catch {
      storage = undefined;
    }

    api = createSession(await r.json(), storage);
    setupCaseTabs();

    el('workbench').hidden = false;
    el('loader').hidden = true;

    render();
    animateLoop();

    el('launch').disabled = false;
    if (navigator.xr?.isSessionSupported) {
      try {
        xrSupported = await navigator.xr.isSessionSupported('immersive-vr');
      } catch {
        xrSupported = undefined;
      }
    }
  } catch (error) {
    el('loader').hidden = false;
    setText('loader', 'The case could not open. Serve the project over HTTP or HTTPS, then reload. ' + error.message);
  }
}

boot();
