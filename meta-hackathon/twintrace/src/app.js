import {createSession} from './model.js';
import {startXR} from './xr.js';

const el = id => document.getElementById(id);
const plot = el('plot');
const context = plot.getContext('2d');
const actions = el('actions');
const steps = ['inspect', 'repair', 'vote', 'done'];
let api;
let angle = -0.48;
let tilt = -0.23;
let drag;
let xrSupported;

function percent(correct, total) {
  return total ? new Intl.NumberFormat('en', {style: 'percent', maximumFractionDigits: 1}).format(correct / total) : '—';
}

function setText(id, value) { el(id).textContent = value; }

function act(action) {
  api.act(action);
  if (action === 'next' || action === 'reset') { angle = -0.48; tilt = -0.23; }
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

function render() {
  const s = api.getState();
  const stageIndex = steps.indexOf(s.stage);
  const inspected = stageIndex > 0;
  const done = s.stage === 'done';
  const showPair = s.stage === 'repair';
  const separate = s.sample.group !== s.neighbor.group;
  setText('opening-score', percent(s.leakyEvalCorrect ?? s.evalCorrect, s.evalTotal));
  setText('case-label', 'CASE / ' + String(s.seed).padStart(3, '0'));
  setText('step-label', done ? 'Review receipt' : 'Release review');
  const headline = {
    inspect: 'Trace the test sample.',
    repair: separate ? 'Similar shapes. Check their origins.' : 'One object, two trays.',
    vote: 'Would you release it?',
    done: s.verdict === 'hold' ? 'Release on hold.' : 'Release approved.'
  };
  const body = {
    inspect: 'Rotate the specimen. Then compare its six measurements with the closest example in the training set.',
    repair: separate ? 'The nearest examples have different source IDs. Compare both origins and check the ledger before confirming the current split.' : 'The samples trace back to the same source. Replace every training twin with a fresh group, keeping the test set and training size fixed.',
    vote: 'The group-disjoint evaluation scored ' + s.evalCorrect + '/' + s.evalTotal + '. Decide whether the evidence supports releasing this classifier for unseen objects.',
    done: 'You chose to ' + (s.verdict === 'hold' ? 'hold' : 'release') + '. The unseen audit scored ' + s.auditCorrect + '/' + s.auditTotal + '. ' + (s.rationale || (s.expectedVerdict === 'release' ? 'The audit supports release for this simulated task.' : 'The audit supports holding release for this simulated task.'))
  };
  setText('stage-title', headline[s.stage]);
  setText('stage-copy', body[s.stage]);
  setText('specimen-id', inspected && s.stage !== 'repair' ? 'Evaluation / ' + s.sample.id : (showPair ? 'Overlay / ' + s.sample.id + ' + ' + s.neighbor.id : 'Evaluation / ' + s.sample.id));
  el('match-key').hidden = !showPair;
  el('pair-detail').hidden = !showPair;
  if (showPair) { setText('sample-group', s.sample.group); setText('neighbor-group', s.neighbor.group); }
  [...el('progress').children].forEach((node, i) => {
    node.classList.toggle('complete', i < stageIndex);
    if (i === stageIndex) node.setAttribute('aria-current','step'); else node.removeAttribute('aria-current');
  });
  setText('eval-value', percent(s.evalCorrect, s.evalTotal));
  setText('eval-count', s.evalCorrect + ' of ' + s.evalTotal + ' correct');
  setText('overlap-value', s.overlap);
  setText('train-value', s.trainCount);
  setText('audit-value', done ? percent(s.auditCorrect, s.auditTotal) : 'Sealed');
  setText('audit-count', done ? s.auditCorrect + ' of ' + s.auditTotal + ' correct' : 'Reveal after your vote');
  actions.replaceChildren();
  if (s.stage === 'inspect') addAction('Find training match →', 'inspect');
  if (s.stage === 'repair') addAction(separate ? 'Confirm current split →' : 'Repair group split →', 'repair');
  if (s.stage === 'vote') { addAction('Hold release', 'hold'); addAction('Approve release', 'release', true); }
  if (done) addAction('Open next case →', 'next');
  draw();
}

// Six circumferential ridges encode the six feature values. The same transform
// projects both specimens, so the overlay compares the observed measurements.
function mesh(features) {
  const rings = [];
  const spokes = 30;
  const profiles = [[-1.18, 0.04],[-1.12,0.20]];
  features.forEach((f, i) => {
    const x = -0.94 + i * .375;
    const radius = .37 + Math.max(0, Math.min(1, f)) * .38;
    profiles.push([x - .112, radius * .66],[x, radius],[x + .105, radius * .82]);
  });
  profiles.push([1.11,.21],[1.18,.04]);
  for (const [x, radius] of profiles) {
    const points = [];
    for (let j = 0; j < spokes; j++) {
      const a = 2 * Math.PI * j / spokes;
      const radial = radius * (1 + .065 * Math.sin(3*a + .6*x) + .035*Math.cos(5*a - x));
      points.push([x, radial*Math.cos(a), radial*Math.sin(a)]);
    }
    rings.push(points);
  }
  return rings;
}

function draw() {
  if (!api || !context || !plot.clientWidth || !plot.clientHeight) return;
  const w = plot.clientWidth, h = plot.clientHeight, dpr = Math.min(devicePixelRatio || 1, 2);
  if (plot.width !== Math.round(w*dpr) || plot.height !== Math.round(h*dpr)) {
    plot.width = Math.round(w*dpr); plot.height = Math.round(h*dpr);
  }
  const c = context;
  c.setTransform(dpr,0,0,dpr,0,0);
  c.clearRect(0,0,w,h);
  const scale = Math.min(w * .295, h * .48);
  const centerX = w*.5, centerY = h*.46;
  const grid = c.createRadialGradient(centerX,centerY,0,centerX,centerY,w*.62);
  grid.addColorStop(0,'#254638'); grid.addColorStop(1,'#112b25');
  c.fillStyle = grid; c.fillRect(0,0,w,h);
  c.fillStyle = '#77947b55';
  for(let x = (w/2)%21; x<w; x+=21) for(let y = (h/2)%21; y<h; y+=21) c.fillRect(x,y,1,1);
  const ca = Math.cos(angle), sa = Math.sin(angle), cb = Math.cos(tilt), sb = Math.sin(tilt);
  function project([x,y,z]) {
    const xx = x*ca + z*sa, zz = z*ca - x*sa;
    const yy = y*cb - zz*sb, depth = y*sb + zz*cb;
    const p = 4.3/(4.3 - depth);
    return [centerX + (xx*.95+yy*.20)*scale*p, centerY + (-yy*.95+xx*.20)*scale*p, depth];
  }
  const s = api.getState();
  const r = mesh(s.sample.features).map(ring => ring.map(project));
  const faces = [];
  for(let i=0;i<r.length-1;i++) {
    for(let j=0;j<r[i].length;j++) {
      const k = (j+1)%r[i].length;
      const p = [r[i][j],r[i+1][j],r[i+1][k],r[i][k]];
      const depth = p.reduce((n,v)=>n+v[2],0)/4;
      faces.push({p,depth});
    }
  }
  faces.sort((a,b)=>a.depth-b.depth);
  for(const {p,depth} of faces) {
    const lum = Math.max(0,Math.min(1,(depth + .6)/1.5));
    c.beginPath(); p.forEach(([x,y],i)=>i?c.lineTo(x,y):c.moveTo(x,y)); c.closePath();
    c.fillStyle = 'rgb('+[49+lum*85,80+lum*86,56+lum*31].map(Math.round).join(',')+')';
    c.fill(); c.lineWidth=.65; c.strokeStyle = 'rgba(201,241,150,'+(.2+.36*lum)+')'; c.stroke();
  }
  if (s.stage==='repair') {
    const other = mesh(s.neighbor.features);
    c.save(); c.strokeStyle='#ffb085'; c.lineWidth=1.6; c.setLineDash([4,3]);
    // Draw the visible half of each ridge. Underlying geometry is unmodified.
    for(let i=3; i<other.length-2; i+=3) {
      const pts=other[i].map(project);
      const mid=pts.reduce((n,v)=>n+v[2],0)/pts.length;
      c.beginPath();
      for(let j=0;j<pts.length;j++) {
        const a=pts[j], b=pts[(j+1)%pts.length];
        if(a[2]>=mid || b[2]>=mid) {c.moveTo(a[0],a[1]);c.lineTo(b[0],b[1]);}
      }
      c.stroke();
    }
    c.restore();
  }
}

function rotate(amount) { angle += amount; draw(); }
plot.addEventListener('pointerdown', e => { if (!api) return; drag = {x:e.clientX,y:e.clientY}; plot.setPointerCapture(e.pointerId); });
plot.addEventListener('pointermove', e => {
  if(!drag) return;
  angle += (e.clientX-drag.x)*.012;
  tilt = Math.max(-1.15, Math.min(1.15, tilt+(e.clientY-drag.y)*.012));
  drag={x:e.clientX,y:e.clientY}; draw();
});
plot.addEventListener('lostpointercapture',()=>{drag=null;});
plot.addEventListener('keydown',e=>{
  if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {e.preventDefault(); rotate(e.key==='ArrowLeft'?-.15:.15);}
});
el('rotate-left').onclick=()=>rotate(-.28);
el('rotate-right').onclick=()=>rotate(.28);
el('reset').onclick=()=>act('reset');
new ResizeObserver(draw).observe(plot);

function xrMessage(message) { const target=el('xr-status'); target.hidden=!message; target.textContent=message; }
el('launch').addEventListener('click',async()=>{
  if (!window.isSecureContext) {xrMessage('VR needs a secure origin. Open the HTTPS judge link, or use localhost for development.'); return;}
  if (!navigator.xr || xrSupported === false) {xrMessage('Immersive VR is unavailable in this browser. Open this same link in Meta Quest Browser with hand tracking enabled. The desktop workshop is ready below.'); return;}
  const b=el('launch'); b.disabled=true;
  try {
    xrMessage('Starting the VR session…');
    // Request the immersive session while the original user activation is alive.
    const session = await startXR(el('xr-canvas'),api);
    const finish = () => { b.disabled=false; xrMessage('Returned from VR. Your workbench is up to date.'); render(); };
    if (session?.addEventListener) {session.addEventListener('end',finish,{once:true});xrMessage('VR session active.');}
    else finish();
  } catch(error) {b.disabled=false; xrMessage('Could not enter VR. '+(error?.message || 'Check headset hand tracking and try again.')); render();}
});
async function boot() {
  try {
    const r=await fetch(new URL('../data/cases.json',import.meta.url));
    if (!r.ok) throw new Error('Unable to load the local case fixtures (' + r.status + ').');
    let storage;
    try {storage=window.localStorage;} catch {storage=undefined;}
    api=createSession(await r.json(),storage);
    el('workbench').hidden=false; el('loader').hidden=true;
    render();
    el('launch').disabled=false;
    if (navigator.xr?.isSessionSupported) {
      try {xrSupported=await navigator.xr.isSessionSupported('immersive-vr');} catch {xrSupported=undefined;}
    }
  } catch(error) {el('loader').hidden=false; setText('loader','The case could not open. Serve the project over HTTP or HTTPS, then reload. '+error.message);}
}
boot();
