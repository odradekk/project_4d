import * as THREE from 'three';
import { U, ps1Material, Pipeline, MAX_BITES } from '../ps1.js';
import { makeTextures } from '../textures.js';
import { Layer } from '../level.js';
import { makePrologueTextures, PROLOGUE_EMISSIVE, PROLOGUE_ALPHA } from './textures.js';
import {
  SECTION_DEFS, SEAMS, W_POS, SPAWN, CAT_ZONE, STARE_SPOT, PERCH, DEEP_CAT, HINGE, FLAP_PARTS, RAMP_FOOT, PIT, HULA,
  worldSolids,
} from './world.js';
import { Prop, PROP_SPAWNS, bakeGroup, Ball4 } from './props.js';
import { aabbSolid, ballSolid, slabSolid } from './four.js';
import { Cat } from './cat.js';
import { Grazer } from './grazer.js';
import { Notebook } from './notebook.js';
import { PrologueSound } from './audio.js';

const $ = (id) => document.getElementById(id);
const EYE = 1.3;
const STOOL_H = 0.45;
const RADIUS = 0.18;
const WALK = 1.25;
const RUN = 2.2;
const CROSS_T = 0.9;
const REACH = 1.9;
const DEEP = 2;
const DUSK_FOG = [0.3, 0.28, 0.36];
// What shows through where the deep room has been eaten: nothing at all.
const VOID = new THREE.Color(0.01, 0.008, 0.015);
const PERCH_POINT = new THREE.Vector3(PERCH.x, PERCH.y + 0.2, PERCH.z);
const PIT_POINT = new THREE.Vector3((PIT.x0 + PIT.x1) / 2, 0, (PIT.z0 + PIT.z1) / 2);
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const approach = (v, t, s) => (v < t ? Math.min(t, v + s) : Math.max(t, v - s));
const lerp = (a, b, t) => a + (b - a) * t;
const angleTo = (from, to) => Math.atan2(Math.sin(to - from), Math.cos(to - from));

// ---------- scene ----------

const renderer = new THREE.WebGLRenderer({ antialias: false });
renderer.setPixelRatio(1);
$('app').appendChild(renderer.domElement);
const pipeline = new Pipeline(renderer);
const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(72, 1, 0.03, 40);
camera.rotation.order = 'YXZ';
scene.add(camera);

const textures = { ...makeTextures(), ...makePrologueTextures() };
const materials = new Map();
function material(tex) {
  if (!materials.has(tex)) {
    const emissive = PROLOGUE_EMISSIVE[tex] ?? (tex === 'bulb' ? 1 : 0);
    materials.set(tex, ps1Material(textures[tex], { emissive, alphaTest: PROLOGUE_ALPHA.has(tex) ? 0.5 : 0 }));
  }
  return materials.get(tex);
}
// Slices of things stood in W use their own textures, in separate materials so they alone can
// glow through the prism.
const sliceMats = new Map();
function sliceMaterial(tex) {
  if (!sliceMats.has(tex)) sliceMats.set(tex, ps1Material(textures[tex]));
  return sliceMats.get(tex);
}

const layers = SECTION_DEFS.map((def, i) => {
  const L = new Layer(i, def, material);
  L.build();
  scene.add(L.group);
  return L;
});

const props = PROP_SPAWNS.map((s) => new Prop(s, material, sliceMaterial, layers));
const wand = props.find((p) => p.id === 'wand');
const nut = props.find((p) => p.id === 'nut');
const stool = props.find((p) => p.id === 'stool');
// Its 4D centre sits halfway between pages Ⅰ and Ⅱ, so both hold a ball of the same size.
const ball = new Ball4({ x: 4.4, z: 3.0, w: 0.21, radius4: 0.25 }, sliceMaterial('ballskin'), material('blobshadow'), layers);

// The cabinet's front is one 4D part: a flap door in Ⅰ, a long board in Ⅱ, on the same hinge line.
// Opening the flap here therefore lowers the board there.
const flapMats = FLAP_PARTS.map((p) => ps1Material(textures[p.tex]));
const flap = {
  open: 0,
  target: 0,
  landed: false,
  parts: FLAP_PARTS.map((p, i) => {
    const pivot = new THREE.Group();
    pivot.position.set(HINGE.x + p.dx, HINGE.y, HINGE.z);
    const g = new THREE.BoxGeometry(p.width, p.length, p.thick);
    g.translate(0, p.length / 2, -p.thick / 2);
    g.setAttribute('color', new THREE.Float32BufferAttribute(new Float32Array(g.attributes.position.count * 3), 3));
    pivot.add(new THREE.Mesh(g, flapMats[i]));
    pivot.rotation.x = -p.closed;
    layers[p.page].group.add(pivot);
    bakeGroup(pivot, layers[p.page]);
    return { ...p, pivot };
  }),
};

// Seams are threads of light that only show through the prism.
const seamMat = ps1Material(textures.seam, { emissive: 1.4, alphaTest: 0.5, side: THREE.DoubleSide });
const seamGroup = new THREE.Group();
scene.add(seamGroup);
const seams = SEAMS.map((s) => {
  const make = (y0, y1) => {
    const g = new THREE.PlaneGeometry(0.12, y1 - y0);
    g.translate(0, (y0 + y1) / 2, 0);
    const col = new Float32Array(g.attributes.position.count * 3).fill(0.6);
    g.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
    g.attributes.uv.array.forEach((v, i, a) => {
      if (i % 2) a[i] = v * (y1 - y0) * 2;
    });
    return new THREE.Mesh(g, seamMat);
  };
  const h = Math.max(layers[s.a].H, layers[s.b].H);
  const whole = make(0, h);
  const stubs = new THREE.Group();
  stubs.add(make(h - 0.5, h), make(0, 0.3));
  const holder = new THREE.Group();
  holder.position.set(s.x, 0, s.z);
  holder.add(whole, stubs);
  seamGroup.add(holder);
  return { ...s, active: false, cut: false, holder, whole, stubs };
});
const seam = (id) => seams.find((s) => s.id === id);

// Through the prism, a dotted outline shows where the nut's hidden ring would come into a page if
// she stood the nut in W now.
const ghostRing = (() => {
  const g = new THREE.TorusGeometry(nut.spec.hidden.radius, 0.012, 4, 24).rotateY(Math.PI / 2);
  g.attributes.uv.array.forEach((v, i, a) => {
    if (i % 2 === 0) a[i] = v * 8;
  });
  g.setAttribute('color', new THREE.Float32BufferAttribute(new Float32Array(g.attributes.position.count * 3).fill(0.5), 3));
  const m = new THREE.Mesh(g, ps1Material(textures.ghostring, { emissive: 1, alphaTest: 0.5, side: THREE.DoubleSide }));
  m.visible = false;
  scene.add(m);
  return m;
})();

// Her hula hoop, leaning on the wall in her room.
{
  const g = new THREE.TorusGeometry(0.36, 0.015, 4, 20).rotateY(Math.PI / 2);
  g.attributes.uv.array.forEach((v, i, a) => {
    if (i % 2 === 0) a[i] = v * 10;
  });
  g.setAttribute('color', new THREE.Float32BufferAttribute(new Float32Array(g.attributes.position.count * 3), 3));
  const hula = new THREE.Mesh(g, material('hulahoop'));
  hula.position.set(HULA.x, HULA.y, HULA.z);
  hula.rotation.z = 0.12;
  layers[0].group.add(hula);
  bakeGroup(hula, layers[0]);
}

const inPit = (x, z, m = 0) => x > PIT.x0 - m && x < PIT.x1 + m && z > PIT.z0 - m && z < PIT.z1 + m;

const cat = new Cat(material);
scene.add(cat.root);
cat.place(0, 5.4, 0, 4.9, -Math.PI / 2);
cat.setShade(layers[0].def.shade);
cat.wander(CAT_ZONE);
cat.avoid = (x, z) => cat.section === 1 && inPit(x, z, 0.12);
const grazer = new Grazer(scene);
const notebook = new Notebook($('nb-page'), ['look', 'flip', 'stand', 'glow', 'question']);
const sound = new PrologueSound();
cat.onMeow = (c) => {
  if (sound.ctx && game.state === 'play' && c.state !== 'held') sound.meow(c.section !== game.section);
};

// ---------- state ----------

const game = {
  state: 'title',
  section: 0,
  x: SPAWN.x,
  z: SPAWN.z,
  yaw: SPAWN.yaw,
  pitch: -0.12,
  mirrored: false,
  peek: 0,
  peekHeld: false,
  peekOn: false,
  dial: 0,
  held: null, // an ordinary object carried in her hand
  held4: null, // an object turned into W, held by its slice in this page
  heldBall: false,
  holdingCat: false,
  onStool: null,
  cross: null,
  moving: false,
  stepPhase: 0,
  focus: null,
  seamNear: null,
  clock: 21 + 2 / 60,
  stage: 'free',
  stageT: 0,
  flags: new Set(),
  timers: [],
  lines: [],
  lineTimer: 0,
  hintTimer: 0,
  cardTimer: 0,
  wandTravel: 0,
  lureT: 0,
  panic: 0,
  dusk: 0,
  flicker: 1,
  fade: 0,
  shake: 0,
  scream: null,
  bites: [],
  grazeT: 0,
  nextBite: 0,
};
window.demo = { game, props, cat, grazer, seams, layers, flap, ball, nut };

// ---------- helpers ----------

function say(text, dur = 3) {
  game.lines.push({ text, dur });
}

function later(sec, fn) {
  game.timers.push({ t: sec, fn });
}

function hint(text, dur = 8) {
  $('hint').textContent = text;
  $('hint').classList.add('show');
  game.hintTimer = dur;
}

function once(flag) {
  if (game.flags.has(flag)) return false;
  game.flags.add(flag);
  return true;
}

function setStage(s) {
  game.stage = s;
  game.stageT = 0;
}

function setMirrored(m) {
  game.mirrored = m;
  $('msg').classList.toggle('mirror', m);
  $('nb-page').classList.toggle('mirror', m);
}


function blocked(section, x, z, r, ignore) {
  if (layers[section].blocked(x, z, r)) return true;
  return props.some(
    (p) => p !== ignore && p.spec.radius && p.state === 'normal' && p.section === section &&
      Math.abs(p.x - x) < p.spec.radius + r && Math.abs(p.z - z) < p.spec.radius + r,
  );
}

const peekTargets = () =>
  [0, 1, 2].filter((s) => s !== game.section).sort((a, b) => Math.abs(W_POS[a] - W_POS[game.section]) - Math.abs(W_POS[b] - W_POS[game.section]));
const peekTarget = () => peekTargets()[game.dial % 2];

const forward = () => [-Math.sin(game.yaw), -Math.cos(game.yaw)];

const camDir = new THREE.Vector3();
const toTarget = new THREE.Vector3();

// Is she looking at page `page` right now, either standing in it or through the prism?
const watching = (page) => (game.section === page ? game.peek < 0.5 : game.peek > 0.8 && peekTarget() === page);

function sees(point, page) {
  if (!watching(page)) return false;
  camera.getWorldDirection(camDir);
  toTarget.subVectors(point, camera.position);
  const d = toTarget.length();
  return d < 7 && toTarget.normalize().dot(camDir) > 0.85;
}

// The yarn ball: the wand's slice in page Ⅱ, when the wand stands in W.
function yarnPos() {
  if (wand.state !== 'standing' || wand.anim) return null;
  const s = wand.sliverIn(1);
  return s ? wand.sliverCenter(s) : null;
}

// The 4D ball resting over the hole in page Ⅱ. The floor of page Ⅰ holds it up, so over there it
// hangs above nothing, solid enough to step on.
function ballBridges() {
  const c = ball.center(1);
  return !game.heldBall && c && c.y < 0.3 && inPit(c.x, c.z);
}

// The nut's ring in page Ⅱ, once the nut stands in W on the floor and she has let go of it.
function hoop() {
  if (nut.state !== 'standing' || nut.anim || game.held4 === nut || nut.pose.P[1] > 0.3) return null;
  const r = nut.ringIn(1);
  return r && { x: r.center.x, z: r.center.z, ax: r.axis.x, az: r.axis.z };
}

// Where the ring would come into a page if she stood the nut in W now, from her hand or from
// where it lies.
function ringPreview() {
  if (nut.state === 'standing' || nut.anim) return null;
  const pose = nut.state === 'held' ? nut.heldPose(game.section, handPoint(), game.yaw + Math.PI / 2) : nut.pose;
  const stood = nut.stoodFrom(pose);
  for (let s = 0; s < W_POS.length; s++) {
    const r = nut.ringIn(s, stood);
    if (r) return r;
  }
  return null;
}

// A vertical plane between her and the object, if there is one: flipping goes round it.
function barrierFor(p) {
  for (const pl of layers[game.section].def.flipPlanes ?? []) {
    const across = pl.axis === 'z' ? [p.z, game.z] : [p.x, game.x];
    const along = pl.axis === 'z' ? p.x : p.z;
    if (along > pl.x0 && along < pl.x1 && (across[0] - pl.at) * (across[1] - pl.at) < 0) return pl;
  }
  return null;
}

// Where she holds something stood in W: a little in front, raised or lowered as she looks.
function handPoint() {
  const [fx, fz] = forward();
  const eye = EYE + (game.onStool ? STOOL_H : 0);
  return new THREE.Vector3(game.x + fx * 0.6, clamp(eye - 0.3 + Math.sin(game.pitch) * 0.7, 0.12, eye + 0.3), game.z + fz * 0.6);
}

// ---------- turning through W: what's in the way ----------

const STATIC_SOLIDS = worldSolids(layers).map(aabbSolid);

// The cabinet front in each page, as it hangs right now.
function flapSolids() {
  return flap.parts.map((p) => {
    p.pivot.updateMatrixWorld(true);
    const at = (x, y, z) => new THREE.Vector3(x, y, z).applyMatrix4(p.pivot.matrixWorld);
    const c = at(0, p.length / 2, -p.thick / 2);
    const edges = [at(p.width / 2, p.length / 2, -p.thick / 2), at(0, p.length, -p.thick / 2), at(0, p.length / 2, 0)];
    return slabSolid(c, edges.map((e) => e.sub(c)), W_POS[p.page]);
  });
}

// Everything a turning object could run into: the rooms and their furniture, the cabinet front,
// the other things lying or standing about, and the ball.
function solidsAround(moving) {
  const out = [...STATIC_SOLIDS, ...flapSolids()];
  for (const p of props) if (p !== moving && p.state !== 'held' && !p.anim) out.push(...p.bodyAt(p.pose));
  if (!game.heldBall) {
    const c = ball.center(0);
    out.push(ballSolid([c.x, c.y, c.z, ball.w], ball.R));
  }
  return out;
}

// What F (stand in W, or lay back down) or R (flip) would turn right now: the object, the pose it
// turns from, the turn, and whether to ignore what it already touches. Null if nothing would turn.
function plannedTurn(key) {
  if (game.cross || game.heldBall) return null;
  const fromHand = (p, make, label) => {
    const from = p.heldPose(game.section, handPoint(), game.yaw + Math.PI / 2);
    return { key, prop: p, from, turn: make(from), ignoreStart: false, label };
  };
  const inPlace = (p, turn, label) => ({ key, prop: p, from: p.pose, turn, ignoreStart: true, label });
  const f = game.focus;
  if (key === 'F') {
    const p = game.held4;
    if (p) return p.section === game.section && !p.anim ? inPlace(p, p.layTurn(p.pose), '放倒') : null;
    if (game.held) return game.held.anim ? null : fromHand(game.held, (from) => game.held.standTurn(from), '竖进第四方向');
    if (f?.type === 'prop' && !f.prop.anim && f.prop !== game.onStool && !barrierFor(f.prop)) {
      return inPlace(f.prop, f.prop.standTurn(f.prop.pose), '竖进第四方向');
    }
    if (f?.type === 'sliver' && !f.prop.anim && f.prop.section === game.section) return inPlace(f.prop, f.prop.layTurn(f.prop.pose), '放倒');
    return null;
  }
  if (game.held4) return null;
  if (game.held) return game.held.anim ? null : fromHand(game.held, (from) => game.held.flipTurn(from), '翻面');
  if (f?.type !== 'prop' || f.prop.anim) return null;
  const pl = barrierFor(f.prop);
  return pl ? inPlace(f.prop, f.prop.acrossTurn(f.prop.pose, pl), `绕着${pl.name}翻过来`) : inPlace(f.prop, f.prop.flipTurn(f.prop.pose), '翻面');
}

// Check a turn before it starts. If it would run into something, say so and leave it unturned.
function refused(plan) {
  if (!plan) return false;
  const hit = plan.prop.sweepHit(plan.from, plan.turn, solidsAround(plan.prop), plan.ignoreStart);
  if (!hit) return false;
  sound.burst({ dur: 0.15, type: 'lowpass', freq: 260, gain: 0.35 });
  const page = W_POS.findIndex((w) => Math.abs(w - hit.piece.c[3]) < 0.08);
  if (hit.piece.ring) say('那个大圆圈转过来，\n会撞到东西。', 3);
  else if (page === game.section) say('转不开……这边太挤了。', 2.5);
  else if (page >= 0) say('转不过去……\n那一页有东西挡着。', 3);
  else say('转不过去……\n好像卡在两页中间了。', 3);
  if (once('sweepHint')) later(3.2, () => hint('举起棱镜看看：转动时它会扫过哪里，那一页里都画成了线，红色的地方会撞上东西。', 9));
  return true;
}

// Through the prism, the way the next turn would go: outlines of the object at steps along the
// turn, in the page being looked into. Pale where the way is clear, red from the step where it
// would run into something.
const preview = {
  key: '',
  wait: 0,
  plan: null,
  hit: null,
  groups: layers.map(() => {
    const g = new THREE.Group();
    scene.add(g);
    return g;
  }),
  clear: new THREE.LineBasicMaterial({ color: 0xc4dcff, depthTest: false }),
  blocked: new THREE.LineBasicMaterial({ color: 0xff3a2a, depthTest: false }),
};

Object.assign(window.demo, { plannedTurn, solidsAround, preview });

function previewPlan() {
  const f = game.focus;
  return plannedTurn('F') ?? (f?.type === 'prop' && barrierFor(f.prop) ? plannedTurn('R') : null);
}

function updatePreview(dt) {
  const plan = game.peek > 0.5 ? previewPlan() : null;
  const key = plan ? `${plan.prop.id}:${plan.label}` : '';
  preview.wait = key === preview.key ? preview.wait - dt : 0;
  preview.key = key;
  if (preview.wait > 0) return;
  preview.wait = 0.15;
  for (const g of preview.groups) {
    for (const line of g.children) line.geometry.dispose();
    g.clear();
  }
  preview.plan = plan;
  preview.hit = plan && plan.prop.sweepHit(plan.from, plan.turn, solidsAround(plan.prop), plan.ignoreStart);
  if (!plan) return;
  const { prop, from, turn } = plan;
  const hit = preview.hit;
  const parts = prop.parts();
  const STEPS = 12;
  W_POS.forEach((c, s) => {
    if (s === game.section) return;
    const clear = [];
    const blocked = [];
    for (let k = 1; k <= STEPS; k++) {
      const pose = from.turned(turn.a, turn.b, (turn.angle * k) / STEPS, turn.pivot);
      (hit && k / STEPS >= hit.step / hit.steps ? blocked : clear).push(...prop.outlineAt(pose, c, parts));
    }
    if (hit) blocked.push(...prop.outlineAt(hit.pose, c, parts));
    if (clear.length) preview.groups[s].add(new THREE.LineSegments(new THREE.BufferGeometry().setFromPoints(clear), preview.clear));
    if (blocked.length) preview.groups[s].add(new THREE.LineSegments(new THREE.BufferGeometry().setFromPoints(blocked), preview.blocked));
  });
}

// ---------- input ----------

const keys = new Set();
const canvas = renderer.domElement;
const lock = () => canvas.requestPointerLock()?.catch?.(() => {});

addEventListener('keydown', (e) => {
  keys.add(e.code);
  if (game.state === 'notebook') {
    if (e.code === 'KeyN' || e.code === 'KeyE' || e.code === 'Escape') closeNotebook();
    if (e.code === 'KeyA' || e.code === 'ArrowLeft') turnPage(-1);
    if (e.code === 'KeyD' || e.code === 'ArrowRight') turnPage(1);
    return;
  }
  if (e.code === 'KeyR' && game.state === 'end') location.reload();
  if (game.state !== 'play' || game.stage === 'scream' || game.stage === 'black') return;
  if (e.code === 'KeyE') onUse();
  if (e.code === 'KeyF') onTurn();
  if (e.code === 'KeyR') onMirror();
  if (e.code === 'KeyN') openNotebook();
  if (e.code === 'KeyQ') turnDial();
  if (e.code === 'Space') onClimb();
});
addEventListener('keyup', (e) => keys.delete(e.code));
addEventListener('contextmenu', (e) => e.preventDefault());

addEventListener('mousedown', (e) => {
  if (game.state === 'title') return start();
  if (game.state === 'pause') return lock();
  if (game.state === 'notebook') {
    if (e.button === 0) turnPage(1);
    return;
  }
  if (game.state !== 'play') return;
  if (e.button === 2) game.peekHeld = true;
  if (e.button === 0 && game.peek > 0.8) tryCross();
});
addEventListener('mouseup', (e) => {
  if (e.button === 2) game.peekHeld = false;
});
let lastWheel = 0;
addEventListener('wheel', (e) => {
  if (game.state === 'notebook') return turnPage(e.deltaY > 0 ? 1 : -1);
  if (e.timeStamp - lastWheel > 200) turnDial();
  lastWheel = e.timeStamp;
});

addEventListener('mousemove', (e) => {
  if (game.state !== 'play' || document.pointerLockElement !== canvas || game.stage === 'scream') return;
  const s = game.mirrored ? -1 : 1;
  game.yaw -= e.movementX * 0.0022 * s;
  game.pitch = clamp(game.pitch - e.movementY * 0.0022, -1.45, 1.45);
});

document.addEventListener('pointerlockchange', () => {
  const locked = document.pointerLockElement === canvas;
  if (!locked && game.state === 'play') {
    game.state = 'pause';
    game.peekHeld = false;
    $('pause').classList.remove('hidden');
  } else if (locked && game.state === 'pause') {
    game.state = 'play';
    $('pause').classList.add('hidden');
  }
});

function start() {
  sound.init();
  $('title').classList.add('hidden');
  game.state = 'play';
  lock();
  $('card').textContent = '二〇〇〇年四月十三日　星期四\n晚上九点';
  $('card').classList.add('show');
  game.cardTimer = 4.5;
  later(1.5, () => hint('WASD 移动　E 拿起 / 互动　N 笔记本\n按住右键 用棱镜看　滚轮 换一页\nF 把东西竖进第四方向 / 放倒　R 翻面', 14));
  later(3, () => say('作业写完了。', 2.5));
  later(6, () => say('煤球，爸爸要十二点才下班呢。\n你小声点，别让邻居听见。', 4));
}

function openNotebook(id) {
  game.state = 'notebook';
  game.peekHeld = false;
  if (id) notebook.show(id);
  else notebook.render();
  $('notebook').classList.remove('hidden');
  sound.paper();
}

function closeNotebook() {
  $('notebook').classList.add('hidden');
  game.state = document.pointerLockElement === canvas ? 'play' : 'pause';
  if (game.state === 'pause') $('pause').classList.remove('hidden');
  sound.paper();
}

function turnPage(d) {
  notebook.turn(d);
  sound.paper();
}

function turnDial() {
  if (game.state !== 'play') return;
  game.dial = (game.dial + 1) % 2;
  sound.burst({ dur: 0.03, type: 'highpass', freq: 4000, gain: 0.12 });
}

function unlockPage(id) {
  if (notebook.unlock(id)) later(1.5, () => hint('笔记本里多了一页（N）', 5));
}

// ---------- actions ----------

function onUse() {
  if (game.cross) return;
  if (game.heldBall) return dropBall();
  if (game.held4) return releaseHeld4();
  if (game.held) return dropHeld();
  // Picking things up and using them waits until she lowers the prism.
  const f = game.peek > 0.3 ? null : game.focus;
  if (!f) return;
  if (f.type === 'prop' || f.type === 'ball') {
    if (game.holdingCat) return say('抱着煤球呢，腾不出手。', 2);
    if (game.onStool) return say('先从凳子上下来。', 2);
  }
  if (f.type === 'ball') {
    game.heldBall = true;
    sound.pickUp();
    if (once('ballPicked')) say('这个皮球也是在铁道边捡的。\n摸起来凉凉的。', 3.5);
    return;
  }
  if (f.type === 'prop') {
    if (barrierFor(f.prop)) {
      say('手伸不进去……栏杆太密了。', 2.5);
      if (game.flags.has('triedBars')) hint('对准它按 R，绕着栏杆把它翻过来。', 8);
      game.flags.add('triedBars');
      return;
    }
    game.held = f.prop;
    f.prop.hold(camera);
    sound.pickUp();
    if (f.prop === nut && once('nutPicked')) say('铁道边捡的螺母。\n比看起来沉。', 3);
    return;
  }
  if (f.type === 'sliver') {
    if (game.onStool) return say('先从凳子上下来。', 2);
    game.held4 = f.prop;
    sound.pickUp();
    return;
  }
  if (f.type === 'cat') return touchCat();
  if (f.type === 'item') {
    if (f.item.kind === 'notebook') return openNotebook();
    if (f.item.kind === 'flap') return openFlap();
    say(f.item.text, 3.5);
  }
}

function dropHeld() {
  const p = game.held;
  const [fx, fz] = forward();
  for (const d of [0.65, 0.5, 0.38]) {
    const x = game.x + fx * d;
    const z = game.z + fz * d;
    if (p.spec.radius ? blocked(game.section, x, z, p.spec.radius, p) : !layers[game.section].walkable(x, z)) continue;
    // Snap to a quarter turn with the front (+z) toward her.
    const yaw = Math.round(game.yaw / (Math.PI / 2)) * (Math.PI / 2);
    p.drop(game.section, x, z, yaw);
    game.held = null;
    sound.putDown();
    return;
  }
  say(game.section === 1 && inPit(game.x + fx * 0.5, game.z + fz * 0.5) ? '放下去就掉进洞里了。' : '这里放不下。', 2);
}

function dropBall() {
  const h = handPoint();
  if (!layers[game.section].walkable(h.x, h.z)) return say('这里放不下。', 2);
  game.heldBall = false;
  ball.place(h.x, h.z);
  sound.putDown();
}

function releaseHeld4() {
  const p = game.held4;
  game.held4 = null;
  p.settle();
  sound.putDown();
}

function onTurn() {
  if (game.cross) return;
  // A 4D ball looks the same however it is turned.
  if (game.heldBall || game.focus?.type === 'ball') return say('怎么转都一样。', 2.5);
  if (game.held4) {
    const p = game.held4;
    if (p.section !== game.section) return say('得回到它原来那一页，\n才能把它放倒。', 3);
    if (p.anim || refused(plannedTurn('F'))) return;
    game.held4 = null;
    p.layDown();
    sound.turn(false);
    return;
  }
  if (game.held) {
    // Turn it into W right out of her hand, and keep hold of its slice.
    const p = game.held;
    if (p.anim || refused(plannedTurn('F'))) return;
    game.held = null;
    p.standFromHand(game.section, handPoint(), game.yaw + Math.PI / 2);
    game.held4 = p;
    sound.turn(true);
    afterStand(p);
    return;
  }
  const f = game.focus;
  if (!f) return;
  if (f.type === 'prop') {
    if (f.prop === game.onStool || barrierFor(f.prop) || refused(plannedTurn('F'))) return;
    f.prop.standUp();
    sound.turn(true);
    afterStand(f.prop);
    return;
  }
  if (f.type === 'sliver') {
    const p = f.prop;
    if (p.section !== game.section) return say('得回到它原来那一页，\n才能把它放倒。', 3);
    if (refused(plannedTurn('F'))) return;
    p.layDown();
    sound.turn(false);
    // Don't let the object appear inside her.
    const dx = game.x - p.x;
    const dz = game.z - p.z;
    const d = Math.hypot(dx, dz);
    const need = (p.spec.radius ?? 0) + RADIUS + 0.02;
    if (p.spec.radius && d < need) {
      game.x = p.x + (dx / (d || 1)) * need;
      game.z = p.z + (dz / (d || 1)) * need;
    }
  }
}

function afterStand(p) {
  if (p.id === 'wand' && once('wandStood')) {
    later(1.5, () => say('毛线球不见了……\n手里只剩一小片。', 3));
    later(4.9, () => say('球跑到那一页去了？', 2.5));
    later(5.7, () => hint('举着棱镜看那一页，拿着它走动、抬高放低试试。', 10));
  } else if (p === nut && once('nutStood')) {
    later(1.5, () => say('螺母竖进去了，\n这边只剩一小片。', 3));
    later(4.7, () => hint('用棱镜看看那一页。', 6));
  } else if (p.id === 'stool' && once('stoolStood')) {
    later(1.5, () => say('凳子不见了，\n地上只剩四个小方块。', 3.5));
  } else if (once('firstStand')) {
    later(1.3, () => say('竖进去了。', 2));
  }
}

function onMirror() {
  if (game.heldBall || game.focus?.type === 'ball') return say('翻不翻都一样。', 2.5);
  const p = game.held ?? (game.focus?.type === 'prop' ? game.focus.prop : null);
  if (!p || game.cross || p.anim || refused(plannedTurn('R'))) return;
  const pl = game.held ? null : barrierFor(p);
  sound.mirrorObject();
  if (pl) {
    p.flipAcross(pl);
    later(1.75, () => sound.putDown());
    if (p.id === 'wand' && once('wandOut')) {
      later(1.9, () => say('出来了！', 2));
      later(4.1, () => say('……上面写的"满满"反了。\n算了，不影响。', 3.5));
    }
    return;
  }
  p.flipMirror();
  if (p.id === 'clock' && once('clockFlip')) later(1.3, () => say('……指针倒着走了。', 3));
  if (p.id === 'pencilbox' && once('boxFlip')) later(1.3, () => say('"好好学习"反过来了。', 3));
  if (p.id === 'rabbit' && once('rabbitFlip')) later(1.3, () => say('兔子的耳朵……换了一边歪。', 3));
  if (p === nut && once('nutFlip')) later(1.3, () => say('螺纹……反着转了？', 3));
}

function onClimb() {
  if (game.cross) return;
  if (game.onStool) return stepDown();
  const f = game.focus;
  if (f?.type !== 'prop' || f.prop.id !== 'stool') return;
  if (game.held || game.held4 || game.heldBall) return say('手里拿着东西呢。', 2);
  if (game.holdingCat) return say('抱着煤球呢。', 2);
  game.onStool = f.prop;
  game.x = f.prop.x;
  game.z = f.prop.z;
  sound.putDown();
}

function stepDown() {
  const s = game.onStool;
  const [fx, fz] = forward();
  for (let a = 0; a < Math.PI * 2; a += Math.PI / 4) {
    const dx = fx * Math.cos(a) - fz * Math.sin(a);
    const dz = fx * Math.sin(a) + fz * Math.cos(a);
    const x = s.x + dx * 0.45;
    const z = s.z + dz * 0.45;
    if (!blocked(game.section, x, z, RADIUS, s)) {
      game.x = x;
      game.z = z;
      game.onStool = null;
      sound.putDown();
      return;
    }
  }
}

function openFlap() {
  if (!game.onStool || Math.hypot(game.x - HINGE.x, game.z - HINGE.z) > 1.0) return say('太高了，够不着。\n得踩着凳子。', 3);
  flap.target = 1;
  sound.burst({ dur: 1.2, freq: 700, sweepTo: 300, q: 8, gain: 0.08 });
  say('翻板门……开了。', 2);
}

function updateFlap(dt) {
  flap.open = approach(flap.open, flap.target, dt / 1.3);
  const e = flap.open * flap.open;
  for (const p of flap.parts) p.pivot.rotation.x = -(p.closed + (e * Math.PI) / 2);
  if (flap.open >= 1 && !flap.landed) {
    flap.landed = true;
    // The board lands in page Ⅱ; from here it is only a muffled knock.
    const far = game.section !== 1;
    sound.burst({ dur: 0.4, type: 'lowpass', freq: far ? 250 : 900, gain: far ? 0.25 : 0.5 });
    sound.tone({ freq: 60, to: 35, dur: 0.4, gain: far ? 0.2 : 0.4 });
    layers[1].colliders.push({ x0: RAMP_FOOT.x - 0.45, z0: RAMP_FOOT.z - 0.05, x1: RAMP_FOOT.x + 0.45, z1: 5.55 });
    for (const p of flap.parts) bakeGroup(p.pivot, layers[p.page]);
    if (watching(1)) later(0.4, () => say('那边的木板放下来了！\n像个滑梯。', 3));
    else later(0.4, () => say('……那边好像有什么东西倒了。', 3));
  }
}

function touchCat() {
  if (cat.state === 'perch') return say('好高……够不着。', 2.5);
  if (game.stage === 'descend' || game.stage === 'pit' || game.stage === 'hop') return say('它往木板上一缩，不让我抱。', 2);
  if (game.stage === 'lure' || game.stage === 'catLost') return say('它玩疯了，抓不住。', 2);
  if (game.stage === 'deep') return grabCat();
  if (game.stage === 'free' || game.stage === 'stare') {
    game.flags.add('petting');
    later(2.5, () => game.flags.delete('petting'));
    cat.meow();
    say(['乖。', '爸爸回来之前，你要藏好哦。', '你的肚子好暖和。'][Math.floor(Math.random() * 3)], 2.5);
  }
}

function grabCat() {
  cat.holdIn(camera);
  game.holdingCat = true;
  setStage('caught');
  say('抓到你了。我们回家。', 3);
  // The way back goes quietly while her back is turned.
  seam('s2').active = false;
  seam('s2').cut = true;
  later(3.5, startPanic);
}

function catIntoSeam() {
  if (game.stage !== 'stare') return;
  setStage('catGone');
  const s = seam('s1');
  cat.leap(s.x, 0.3, s.z, 0.45, () => {
    cat.place(1, PERCH.x, PERCH.y, PERCH.z, PERCH.yaw);
    cat.setShade(layers[1].def.shade);
    cat.sitFacing(PERCH.yaw);
    cat.state = 'perch';
    sound.burst({ dur: 0.3, freq: 2500, sweepTo: 400, q: 3, gain: 0.12 });
    setStage('perched');
  });
  later(0.6, () => say('煤球？！', 2));
  later(3.2, () => say('它……钻进去了？', 3));
}

// Lured by the yarn, the cat comes down the board, and stops where the floor has fallen in.
function catDescends() {
  setStage('descend');
  cat.lookTarget = null;
  cat.leap(RAMP_FOOT.x, HINGE.y + 0.03, HINGE.z - 0.12, 0.45, () => {
    cat.walkTo(RAMP_FOOT.x, 0, RAMP_FOOT.z, 0.9, () => {
      cat.sitFacing(Math.PI);
      setStage('pit');
    });
  });
  later(0.8, () => say('下来了！顺着木板下来了！', 3));
}

// Over the ball, which hangs above the hole without falling, and on into the room.
function crossPit() {
  setStage('hop');
  const c = ball.center(1);
  cat.lookTarget = null;
  cat.leap(c.x, c.y + ball.partIn(1).r, c.z, 0.4, () => {
    later(0.3, () => cat.leap(c.x, 0, PIT.z0 - 0.3, 0.4, () => {
      setStage('lure');
      game.lureT = 0;
      cat.chase(yarnPos);
    }));
  });
  later(0.5, () => say('踩着皮球跳过来了！', 2.5));
  later(4, () => hint('用毛线球把煤球引回缝那边，带它回家。', 9));
}

// It never passes up a hoop: round to the near side, then through.
function hoopJump(h) {
  const side = Math.sign((cat.pos.x - h.x) * h.ax + (cat.pos.z - h.z) * h.az) || 1;
  const from = { x: h.x + h.ax * side * 0.6, z: h.z + h.az * side * 0.6 };
  const to = { x: h.x - h.ax * side * 0.6, z: h.z - h.az * side * 0.6 };
  const clear = (p) => !layers[1].blocked(p.x, p.z, 0.15);
  if (!clear(from) || !clear(to) || inPit(h.x, h.z, 0.1)) return;
  cat.goTo(from.x, from.z, 1.1, () => {
    cat.leap(to.x, 0, to.z, 0.5, () => {
      game.hoopCd = 3;
      cat.chase(yarnPos);
    });
  });
  if (once('hoopJump')) {
    later(1.2, () => say('钻过去了！', 2));
    later(3.6, () => say('要是把圈立在缝跟前……\n它一跳，就能跳回家了。', 3.5));
  }
}

// Lined up for the hoop at the seam home, it stops and stares at something in the air short of the
// ring, then jumps anyway: into a seam she cannot see from her room.
function catIntoS2(h) {
  setStage('catLost');
  const s2 = seam('s2');
  const dx = h.x - s2.x;
  const dz = h.z - s2.z;
  const d = Math.hypot(dx, dz) || 1;
  cat.goTo(s2.x - (dx / d) * 0.5, s2.z - (dz / d) * 0.5, 1.0, () => {
    cat.sitFacing(Math.atan2(dx, dz));
    cat.lookTarget = new THREE.Vector3(s2.x, 0.6, s2.z);
    if (watching(1)) say('要跳了……\n煤球？你看什么呢？', 2.5);
    later(1.8, () => {
      cat.lookTarget = null;
      s2.active = true;
      cat.leap(s2.x, 0.45, s2.z, 0.35, () => {
        cat.place(DEEP, DEEP_CAT.x, 0, DEEP_CAT.z, Math.PI);
        cat.setShade(layers[DEEP].def.shade);
        cat.sitFacing(Math.PI * 0.8);
        sound.burst({ dur: 0.3, freq: 2500, sweepTo: 400, q: 3, gain: 0.12 });
      });
      later(0.7, () => say(watching(1) ? '煤球！' : '……煤球呢？', 2.5));
      later(3.5, () => say('它跳到一半……不见了。', 3));
      setStage('chase');
    });
  });
}

function nearSeam() {
  let best = null;
  let bestD = 1.1;
  for (const s of seams) {
    if (!s.active || (s.a !== game.section && s.b !== game.section)) continue;
    const d = Math.hypot(game.x - s.x, game.z - s.z);
    if (d < bestD) {
      best = s;
      bestD = d;
    }
  }
  return best;
}

function tryCross() {
  const s = game.seamNear;
  if (!s || game.cross) return;
  if (game.onStool) return say('先从凳子上下来。', 2);
  if (game.held4) return say('两只手都要握着棱镜，\n竖进去的东西带不过去。', 3);
  if (game.heldBall) return say('两只手都要握着棱镜，\n皮球带不过去。', 3);
  if (game.held && !game.held.spec.small) return say('两只手都要握着棱镜，\n凳子带不过去。', 3);
  const to = s.a === game.section ? s.b : s.a;
  game.cross = { t: 0, to, seam: s, swapped: false };
  game.peekHeld = false;
  sound.flip();
}

function updateCross(dt) {
  const C = game.cross;
  if (!C) return;
  C.t += dt;
  if (!C.swapped && C.t >= CROSS_T / 2) {
    C.swapped = true;
    game.section = C.to;
    game.x = C.seam.x;
    game.z = C.seam.z;
    if (C.seam.mirror) setMirrored(!game.mirrored);
    sound.setLayer(C.to);
    onEnter(C.to);
  }
  if (C.t >= CROSS_T) game.cross = null;
}

function onEnter(section) {
  if (section === 1 && once('enteredEmpty')) {
    later(1.2, () => say('……好安静。', 2.5));
    later(4, () => say('这是我的房间？\n什么都没有了。', 3.5));
    unlockPage('seam');
  }
  if (section === DEEP && once('enteredDeep')) {
    setStage('deep');
    later(1, () => say('咦，反过来了。', 2.5));
    later(4.5, () => say('……这是我的房间吗？', 3));
    later(8.5, () => say('好大。窗外好亮。', 3));
  }
}

function startPanic() {
  setStage('panic');
  say('……缝呢？', 3);
  sound.drain();
  game.flags.add('panic');
  later(4, () => say('缝不见了……', 2.5));
  later(9, () => say('刚才明明就在这儿的……', 3));
  later(15, () => say('爸爸？', 2));
  later(20, () => say('爸爸！我在这儿！', 3));
  later(28, () => say('我要回家……', 3));
}

// ---------- update ----------

function updateTimers(dt) {
  for (const T of game.timers) T.t -= dt;
  const due = game.timers.filter((T) => T.t <= 0);
  game.timers = game.timers.filter((T) => T.t > 0);
  for (const T of due) T.fn();
}

function updateLines(dt) {
  if (game.lineTimer > 0) {
    game.lineTimer -= dt;
    if (game.lineTimer <= 0.35) $('msg').classList.remove('show');
    return;
  }
  const next = game.lines.shift();
  if (!next) return;
  $('msg').textContent = next.text;
  $('msg').classList.add('show');
  game.lineTimer = next.dur + 0.35;
}

function updateMove(dt) {
  game.moving = false;
  if (game.cross || game.onStool || game.stage === 'scream' || game.stage === 'black') return;
  let f = 0;
  let s = 0;
  if (keys.has('KeyW')) f += 1;
  if (keys.has('KeyS')) f -= 1;
  if (keys.has('KeyD')) s += 1;
  if (keys.has('KeyA')) s -= 1;
  if (game.mirrored) s = -s;
  const len = Math.hypot(f, s);
  if (!len) return;
  const running = keys.has('ShiftLeft') || keys.has('ShiftRight');
  const speed = (running ? RUN : WALK) * (game.peek > 0.5 ? 0.5 : 1);
  const [fx, fz] = forward();
  const rx = Math.cos(game.yaw);
  const rz = -Math.sin(game.yaw);
  const dx = ((fx * f + rx * s) / len) * speed * dt;
  const dz = ((fz * f + rz * s) / len) * speed * dt;
  game.moving = true;
  if (!blocked(game.section, game.x + dx, game.z, RADIUS)) game.x += dx;
  if (!blocked(game.section, game.x, game.z + dz, RADIUS)) game.z += dz;
  const before = game.stepPhase;
  game.stepPhase += speed * dt * 2.8;
  if (Math.floor(before / Math.PI) !== Math.floor(game.stepPhase / Math.PI)) sound.step(running, ',');
}

// Moving her hand moves every slice of what she holds, in every page at once.
function updateHeld4() {
  const p = game.held4;
  if (!p) return;
  const h = handPoint();
  const moved = h.distanceTo(p.position);
  p.moveTo(h);
  if (p === wand && p.state === 'standing' && !p.anim) {
    game.wandTravel += moved;
    if (game.wandTravel > 1.5) unlockPage('link');
  }
}

// The ball is one object in two pages: in her hand here, and at the same spot over there.
function updateHeldBall() {
  if (!game.heldBall) return;
  const h = handPoint();
  const before = ball.center(game.section);
  ball.setCenter(h);
  if (before && watching(1)) game.ballMoved = (game.ballMoved ?? 0) + h.distanceTo(before);
}

// Small discoveries around the two things from the railway; seeing both earns a notebook page.
function updateDiscoveries() {
  const here = ball.center(0);
  const there = ball.center(1);
  // Its lowest point lies between the pages, so in her room it rests a little above the floor.
  if (!game.heldBall && here.y < 0.3 && sees(here, 0) && camera.position.distanceTo(here) < 2 && once('ballHover')) {
    say('皮球没贴着地板……\n底下空着一点点。', 3);
  }
  if (there && game.section === 0 && sees(there, 1) && once('ballThere')) say('那一页……也有一个皮球？', 3);
  if (game.flags.has('ballThere') && (game.ballMoved ?? 0) > 0.4 && once('ballLinked')) {
    say('我动这个，那个也跟着动。\n它们是同一个皮球？', 3.5);
  }
  if (sees(PIT_POINT, 1) && once('pitSeen')) say('那一页的地上……塌了一个大洞？', 3);
  if (ballBridges() && sees(there, 1) && once('ballBridge')) {
    say(`皮球停在洞上面，没掉下去！\n……是${game.section === 0 ? '我这边' : '那一页'}的地板托着它。`, 4);
  }
  const ghost = game.ghost;
  if (ghost && game.section === 0 && sees(ghost.center, ghost.section) && once('ghostSeen')) {
    say('螺母那儿……那一页有个圆圈的影子？', 3);
  }
  const ring = nut.state === 'standing' && !nut.anim && nut.sliverIn(1);
  if (ring && sees(nut.sliverCenter(ring), 1) && once('ringSeen')) say('真的有个大圆圈！\n它跟螺母是一个东西？', 3.5);
  if (game.flags.has('ballThere') && game.flags.has('ringSeen')) unlockPage('kinds');
}

function updatePeek(dt) {
  const want = game.peekHeld && !game.cross && game.stage !== 'scream';
  if (want !== game.peekOn) {
    game.peekOn = want;
    sound.peek(want);
  }
  game.peek = approach(game.peek, want ? 1 : 0, dt * 4);
  if (game.peek > 0.8) game.flags.add('peeked');
}

function updateFocus() {
  game.focus = null;
  game.seamNear = nearSeam();
  // She can keep aiming at something while she looks at the other page through the prism.
  if (game.cross || game.held4 || game.heldBall) return;
  camera.getWorldDirection(camDir);
  const cands = [];
  const ballHere = ball.center(game.section);
  if (ballHere) cands.push({ type: 'ball', at: ballHere });
  for (const p of props) {
    if (p.anim) continue;
    // A stood-up object belongs to no single page; its slices can be taken hold of wherever they lie.
    const sl = p.state === 'standing' && p.sliverIn(game.section);
    if (sl) cands.push({ type: 'sliver', prop: p, at: p.sliverCenter(sl) });
    if (p.section === game.section && p.state === 'normal' && p !== game.onStool) cands.push({ type: 'prop', prop: p, at: p.center() });
  }
  if (!game.holdingCat && cat.section === game.section && cat.state !== 'jump' && cat.state !== 'run') {
    cands.push({ type: 'cat', at: cat.pos.clone().add(new THREE.Vector3(0, 0.2, 0)) });
  }
  for (const it of layers[game.section].items) {
    if (it.kind === 'flap' && flap.target > 0) continue;
    cands.push({ type: 'item', item: it, at: new THREE.Vector3(it.x, it.y, it.z) });
  }
  // Aim matters most; among things she's looking at, the nearer one wins.
  let best = -Infinity;
  for (const c of cands) {
    toTarget.subVectors(c.at, camera.position);
    const dist = toTarget.length();
    if (dist > REACH) continue;
    const dot = toTarget.normalize().dot(camDir);
    if (dot < 0.86) continue;
    const score = dot - dist * 0.04;
    if (score > best) {
      best = score;
      game.focus = c;
    }
  }
}

function promptText() {
  if (game.state !== 'play' || game.cross) return '';
  if (game.peek > 0.5) {
    const plan = preview.plan;
    return [
      game.seamNear ? '[左键] 钻过去' : '',
      plan ? `[${plan.key}] ${plan.label}${preview.hit ? '（会撞到东西）' : ''}` : '',
    ].filter(Boolean).join('　');
  }
  if (game.stage === 'scream' || game.stage === 'black') return '';
  const parts = [];
  const f = game.focus;
  if (game.heldBall) {
    parts.push('[E] 放下皮球', '[F] 竖进第四方向', '[R] 翻面');
  } else if (game.held4) {
    parts.push('[E] 松手');
    if (game.held4.section === game.section) parts.push('[F] 放倒');
  } else if (game.held) {
    parts.push(`[E] 放下${game.held.name}`, '[F] 竖进第四方向', '[R] 翻面');
  } else if (f?.type === 'prop') {
    const pl = barrierFor(f.prop);
    if (pl) parts.push(`[R] 绕着${pl.name}翻过来`);
    else {
      parts.push(`[E] 拿起${f.prop.name}`, '[F] 竖进第四方向', '[R] 翻面');
      if (f.prop.id === 'stool' && !game.onStool) parts.push('[空格] 站上去');
    }
  } else if (f?.type === 'ball') {
    parts.push('[E] 拿起皮球', '[F] 竖进第四方向', '[R] 翻面');
  } else if (f?.type === 'sliver') {
    parts.push(`[E] 抓住${f.prop.name}`);
    if (f.prop.section === game.section) parts.push('[F] 放倒');
  } else if (f?.type === 'cat') {
    parts.push(game.stage === 'deep' ? '[E] 抱煤球' : '[E] 摸摸煤球');
  } else if (f?.type === 'item') {
    parts.push(`[E] ${f.item.prompt}`);
  }
  if (game.onStool) parts.push('[空格] 下来');
  return parts.join('　');
}

function updateStage(dt) {
  game.stageT += dt;
  const T = game.stageT;
  switch (game.stage) {
    case 'free':
      if ((game.flags.has('peeked') && T > 18) || T > 55) {
        setStage('stare');
        seam('s1').active = true;
        cat.goTo(STARE_SPOT.x, STARE_SPOT.z, 0.4, () => {
          cat.sitFacing(STARE_SPOT.yaw);
          cat.state = 'stare';
          say('煤球，你在看什么？', 3);
          later(5, () => {
            if (game.stage === 'stare' && !game.flags.has('sawSeam')) hint('猫好像看见了什么。用棱镜看看那个墙角？', 8);
          });
        });
      }
      break;
    case 'stare': {
      const s = seam('s1');
      toTarget.set(s.x - camera.position.x, 1.0 - camera.position.y, s.z - camera.position.z);
      const d = toTarget.length();
      camera.getWorldDirection(camDir);
      if (game.peek > 0.8 && d < 5 && toTarget.normalize().dot(camDir) > 0.88 && once('sawSeam')) {
        say('那是……一道缝？', 3);
        later(3.5, catIntoSeam);
      }
      if (T > 40 && !game.flags.has('sawSeam') && cat.state === 'stare') catIntoSeam();
      break;
    }
    case 'perched': {
      const yarn = yarnPos();
      cat.lookTarget = yarn && yarn.distanceTo(cat.pos) < 4 ? yarn : null;
      if (sees(PERCH_POINT, 1) && once('sawPerch')) {
        say('煤球！你怎么跑到那边的柜子上去了？', 3);
        later(3.4, () => say('太高了……它不敢跳下来。', 3));
        later(7, () => say('要是能把它引下来……\n逗猫棒！爸爸把它收到窗台上了。', 4));
      }
      if (T > 15 && !game.flags.has('sawPerch') && once('findCatHint')) hint('煤球去哪儿了？用棱镜看看那一页。', 8);
      if (cat.lookTarget && once('catSeesBall')) {
        say('它看见球了！', 2.5);
        if (flap.target < 1) later(3, () => say('可它还是不敢跳下来……\n太高了。', 3));
      }
      // The key moments only happen while she is looking into page Ⅱ, so they are never missed.
      if (flap.landed && yarn && watching(1) && cat.state === 'perch' && Math.hypot(yarn.x - RAMP_FOOT.x, yarn.z - RAMP_FOOT.z) < 2.5) catDescends();
      if (T > 60 && !game.flags.has('wandOut') && once('barsHint')) hint('逗猫棒在窗台的防护栏后面。手伸不进去，就翻翻笔记本里"翻面"那一页。', 10);
      if (game.flags.has('catSeesBall') && flap.target < 1 && game.flags.has('wandStood') && T > 90 && once('flapHint')) {
        hint('透过棱镜看看吊柜，两边的样子好像不一样……', 9);
      }
      break;
    }
    case 'pit': {
      const yarn = yarnPos();
      cat.lookTarget = yarn && yarn.distanceTo(cat.pos) < 4 ? yarn : PIT_POINT;
      if (watching(1) && once('catStuck')) {
        say('……它停住了。', 2);
        say('木板前面的地塌了一大块，\n它跳不过去。', 3.5);
        later(8, () => game.stage === 'pit' && !ballBridges() && say('要是洞中间有个能踩的地方……', 3));
      }
      // An ordinary thing stood in W is only a paper-thin cut over there.
      const seat = stool.state === 'standing' && !stool.anim && stool.sliverIn(1);
      if (seat && watching(1)) {
        const c = stool.sliverCenter(seat);
        if (inPit(c.x, c.z) && once('stoolInPit')) say('凳子到那边只剩薄薄一片，\n像张纸，煤球不敢踩。', 3.5);
      }
      // Once the ball is over the hole, it eyes the ball for a moment, then goes.
      game.bridgeT = ballBridges() && watching(1) ? (game.bridgeT ?? 0) + dt : 0;
      if (game.bridgeT > 0) cat.lookTarget = ball.center(1);
      if (game.bridgeT > 3) crossPit();
      if (T > 40 && once('pitHint')) hint('这边的东西竖进去，到那一页只剩薄薄一片，踩不住。\n有没有在那一页也是完整的东西？', 10);
      if (T > 80 && once('ballHint')) hint('床边的皮球，用棱镜看看它在那一页是什么样子。', 8);
      break;
    }
    case 'lure': {
      const yarn = yarnPos();
      const chasing = cat.state === 'chase' && watching(1);
      if (chasing) game.lureT += dt;
      game.hoopCd = Math.max(0, (game.hoopCd ?? 0) - dt);
      const s1 = seam('s1');
      if (chasing && Math.hypot(cat.pos.x - s1.x, cat.pos.z - s1.z) < 1.1 && once('catAtSeam')) {
        game.seamT = 0;
        say('到缝跟前了！可它只顾着扑毛线球，\n不往缝里跳。', 3.5);
        say('煤球只有钻圈的时候才肯跳起来……\n呼啦圈又带不过去。', 4);
      }
      if (game.flags.has('catAtSeam')) {
        game.seamT += dt;
        if (game.seamT > 25 && once('hoopHint')) hint('那一页有没有一个能让煤球钻的圈？', 8);
        if (game.seamT > 60 && once('nutHint')) hint('把螺母竖进第四方向，那一页就有个大圆圈。\n把圈立在缝跟前，圈口对着屋里。', 10);
      }
      // Bring the yarn to the ring and it jumps through. At the seam home, the jump goes wrong.
      const h = hoop();
      if (!h || !yarn || !chasing || game.hoopCd > 0) break;
      if (Math.hypot(yarn.x - h.x, yarn.z - h.z) > 1.0 || Math.hypot(cat.pos.x - h.x, cat.pos.z - h.z) > 2.5) break;
      if (Math.hypot(h.x - s1.x, h.z - s1.z) < 0.5) {
        const s2 = seam('s2');
        const dx = h.x - s2.x;
        const dz = h.z - s2.z;
        if (Math.abs(h.ax * dx + h.az * dz) / (Math.hypot(dx, dz) || 1) > 0.7) catIntoS2(h);
        else if (once('ringSideways')) say('圈是侧着的……\n得让圈口对着煤球。', 3);
        break;
      }
      hoopJump(h);
      break;
    }
    case 'chase':
      if (T > 18 && once('chaseHint')) hint('煤球钻进了另一道缝。到那间空屋子里，用棱镜找找看。', 8);
      break;
    case 'deep':
      if (T > 25 && once('deepHint')) hint('把煤球抱起来，带它回家。', 6);
      break;
    case 'panic':
      game.panic = clamp(T / 25, 0, 1);
      game.dusk = clamp(T / 22, 0, 1);
      if (game.peek > 0.8 && peekTarget() === 0 && once('sawHome')) say('那是我的房间……\n台灯还亮着。', 3.5);
      if (T > 14 && !grazer.active) startGrazer();
      break;
    case 'scream':
      updateScream(dt);
      break;
    case 'black':
      if (T > 3 && game.state === 'play') {
        game.state = 'end';
        $('end-text').textContent = '— 序章　完 —\n\n试玩到此结束';
        $('end').classList.remove('hidden');
        document.exitPointerLock?.();
      }
      break;
    default:
      break;
  }
  if (grazer.active) updateGrazer(dt);
}

function startGrazer() {
  grazer.active = true;
  grazer.x = 7.6;
  grazer.y = 1.55;
  grazer.z = 5.2;
  grazer.yaw = Math.atan2(6.8 - grazer.x, 7.5 - grazer.z);
  grazer.w = W_POS[DEEP] + 1.15;
  game.grazeT = 0;
  game.nextBite = 0;
}

function addBite(x, y, z, yaw, hx, hy, hz) {
  if (game.bites.length >= MAX_BITES) return;
  game.bites.push({ x, y, z, yaw, hx, hy, hz, g: 0 });
  sound.bite();
}

function updateGrazer(dt) {
  game.grazeT += dt;
  const T = game.grazeT;
  grazer.update(dt);
  const wFinal = W_POS[DEEP] - 0.05;
  // Coming in along W: a few scattered lumps first, then the mouth, then the whole thing.
  const e = clamp(T / 12, 0, 1);
  grazer.w = lerp(W_POS[DEEP] + 1.15, wFinal, 1 - (1 - e) * (1 - e));
  if (T > 12 && T < 13 && once('bite1')) addBite(6.8, 1.15, 7.45, 0, 0.62, 1.2, 0.5);
  if (T > 13.6 && once('bite2')) addBite(7.35, 1.7, 7.25, 0.4, 0.7, 1.2, 0.6);
  if (T > 15 && game.stage === 'panic') {
    const dx = game.x - grazer.x;
    const dz = game.z - grazer.z;
    const want = Math.atan2(dx, dz);
    grazer.yaw += angleTo(grazer.yaw, want) * Math.min(1, dt * 0.8);
    const d = Math.hypot(dx, dz);
    if (d > 0.5) {
      grazer.x += Math.sin(grazer.yaw) * 0.38 * dt;
      grazer.z += Math.cos(grazer.yaw) * 0.38 * dt;
    }
    game.nextBite -= dt;
    if (game.nextBite <= 0) {
      game.nextBite = 1.7;
      const m = grazer.mouth();
      const [fx, fz] = grazer.forward;
      addBite(m.x + fx * 0.35, 1.45, m.z + fz * 0.35, grazer.yaw, 0.8, 1.2, 0.55);
    }
    const m = grazer.mouth();
    const md = Math.hypot(game.x - m.x, game.z - m.z);
    if (md < 2.8 && game.holdingCat && once('catFlees')) {
      game.holdingCat = false;
      cat.release(scene);
      cat.place(DEEP, game.x, 0.5, game.z, game.yaw);
      sound.hiss();
      cat.leap(game.x - Math.sin(game.yaw) * 0.8, 0, game.z - Math.cos(game.yaw) * 0.8, 0.4, () => cat.goTo(1.0, 1.0, 2.2, () => cat.sitFacing(Math.PI * 0.75)));
      say('煤球——！', 1.6);
    }
    if ((md < 2.0 || T > 55) && game.stage === 'panic') startScream();
  }
}

function startScream() {
  setStage('scream');
  game.peekHeld = false;
  game.scream = sound.scream();
}

function updateScream(dt) {
  const m = grazer.mouth();
  const wantYaw = Math.atan2(-(m.x - game.x), -(m.z - game.z));
  const wantPitch = Math.atan2(m.y - EYE, Math.hypot(m.x - game.x, m.z - game.z));
  game.yaw += angleTo(game.yaw, wantYaw) * Math.min(1, dt * 5);
  game.pitch += (wantPitch - game.pitch) * Math.min(1, dt * 5);
  // It stops just out of reach and opens, so the last thing she sees is the whole mouth.
  grazer.yaw += angleTo(grazer.yaw, Math.atan2(game.x - grazer.x, game.z - grazer.z)) * Math.min(1, dt * 4);
  grazer.open = clamp(game.stageT / 1.2, 0, 1);
  game.shake = 0.6;
  if (game.stageT > 1.5) {
    game.scream?.cut();
    game.scream = null;
    game.fade = 1;
    sound.silence();
    setStage('black');
  }
}

function updateBites(dt) {
  game.bites.forEach((b, i) => {
    b.g = Math.min(1, b.g + dt * 3);
    const e = 1 - (1 - b.g) ** 3;
    U.uBites.value[i].set(b.x, b.y, b.z, b.yaw);
    U.uBiteSize.value[i].set(b.hx * e, b.hy * e, b.hz * e, 0);
  });
}

function updateCat(dt) {
  if (game.holdingCat) cat.updateHeld(dt);
  else cat.update(dt);
}

function updateFlicker() {
  const prox = grazer.active ? grazer.proximity(game.x, EYE, game.z, W_POS[game.section]) : 0;
  game.prox = prox;
  game.flicker = prox > 0.4 && Math.random() < prox * 0.25 ? 0.4 + Math.random() * 0.4 : 1;
}

function updateHud(dt) {
  game.hintTimer -= dt;
  if (game.hintTimer <= 0) $('hint').classList.remove('show');
  game.cardTimer -= dt;
  if (game.cardTimer <= 0) $('card').classList.remove('show');
  $('prompt').textContent = promptText();
  $('cross').classList.toggle('hidden', game.peek > 0.3 || game.stage === 'scream' || game.stage === 'black');
  $('dial').classList.toggle('on', game.peek > 0.5);
  const tgt = peekTarget();
  for (const span of $('dial').children) {
    const l = Number(span.dataset.l);
    span.className = [l === game.section ? 'cur' : '', l === tgt ? 'tgt' : ''].join(' ');
  }
}

function update(dt) {
  U.uTime.value += dt;
  game.clock += dt / 3600;
  updateTimers(dt);
  updateLines(dt);
  updateCross(dt);
  updateMove(dt);
  updateHeld4();
  updateHeldBall();
  updatePeek(dt);
  updateStage(dt);
  updateFlap(dt);
  updateCat(dt);
  for (const p of props) {
    p.update(dt);
    p.setTime(game.clock);
  }
  updateBites(dt);
  game.ghost = ringPreview();
  updateDiscoveries();
  updateFocus();
  updatePreview(dt);
  updateFlicker();
  updateHud(dt);
  game.shake = Math.max(0, game.shake - dt * 2);
  sound.update(dt, {
    page: game.section,
    calm: !game.flags.has('panic'),
    purr: game.holdingCat || game.flags.has('petting'),
    panic: game.panic,
    grazer: game.prox ?? 0,
    danger: Math.max(game.panic * 0.8, game.prox ?? 0),
  });
}

// ---------- render ----------

function applySection(i, isPeek) {
  const d = layers[i].def;
  const dusk = i === DEEP ? game.dusk : 0;
  U.uFogColor.value.setRGB(...d.fog.map((v, k) => lerp(v, DUSK_FOG[k], dusk)));
  U.uFogNear.value = lerp(d.fogNear, 2, dusk);
  U.uFogFar.value = lerp(d.fogFar, 11, dusk);
  U.uAmbient.value.setRGB(...d.ambient.map((v) => v * (1 - 0.6 * dusk)));
  U.uLightColor.value.setRGB(...d.glow);
  U.uLightRange.value = 3.2;
  U.uGlobal.value = (1 - 0.55 * dusk) * game.flicker;
  U.uBiteCount.value = i === DEEP ? game.bites.length : 0;
  for (const L of layers) L.group.visible = L.index === i;
  cat.root.visible = game.holdingCat ? !isPeek : cat.section === i;
  if (game.held) game.held.group.visible = !isPeek;
  grazer.meshes[0].visible = !isPeek && grazer.active;
  grazer.meshes[1].visible = isPeek && grazer.active;
  seamGroup.visible = isPeek;
  preview.groups.forEach((g, k) => (g.visible = isPeek && k === i));
  // Through the prism, things that run through several pages glow.
  for (const m of sliceMats.values()) m.uniforms.uEmissive.value = isPeek ? 0.35 : 0.06;
  for (const m of flapMats) m.uniforms.uEmissive.value = isPeek ? 0.3 : 0;
  // The dotted outline of where the nut's ring would come in, only through the prism.
  const ghost = game.ghost;
  ghostRing.visible = isPeek && ghost?.section === i && preview.plan?.prop !== nut;
  if (ghostRing.visible) {
    ghostRing.position.copy(ghost.center);
    ghostRing.rotation.y = Math.atan2(-ghost.axis.z, ghost.axis.x);
  }
}

function render() {
  const onStool = game.onStool ? STOOL_H : 0;
  const bob = game.moving ? Math.sin(game.stepPhase) * 0.022 : 0;
  const sh = game.shake * 0.03;
  camera.position.set(game.x, EYE + onStool + bob, game.z);
  camera.rotation.set(game.pitch + (Math.random() - 0.5) * sh, game.yaw + (Math.random() - 0.5) * sh, 0);
  camera.updateMatrixWorld();
  U.uLightPos.value.set(game.x, EYE + onStool - 0.2, game.z);

  // Seams face the camera and show only what the prism can see from here.
  for (const s of seams) {
    const here = s.a === game.section || s.b === game.section;
    s.holder.visible = here && (s.active || s.cut);
    s.whole.visible = s.active;
    s.stubs.visible = s.cut;
    s.holder.rotation.y = Math.atan2(game.x - s.x, game.z - s.z);
  }

  const clearFor = (s) => (s === DEEP ? VOID : U.uFogColor.value);
  grazer.slice(0, W_POS[game.section]);
  applySection(game.section, false);
  pipeline.renderTo(pipeline.rtMain, scene, camera, clearFor(game.section));

  const target = peekTarget();
  if (game.peek > 0.01) {
    grazer.slice(1, W_POS[target]);
    applySection(target, true);
    pipeline.renderTo(pipeline.rtPeek, scene, camera, clearFor(target));
  }

  const P = pipeline.post.uniforms;
  const C = game.cross;
  P.uPeek.value = game.peek;
  P.uMirror.value = game.mirrored ? 1 : 0;
  P.uSqueeze.value = C ? Math.max(0.002, Math.abs(Math.cos((Math.PI * C.t) / CROSS_T))) : 1;
  P.uTime.value = U.uTime.value;
  P.uNoise.value = (game.prox ?? 0) * 0.3 + game.panic * 0.1 + (C ? 0.8 : 0) + game.shake * 0.15;
  P.uWarp.value = (game.prox ?? 0) * 0.8 + (C ? 2 : 0);
  P.uDamage.value = 0;
  P.uDesat.value = game.panic * 0.25;
  P.uFade.value = game.fade;
  P.uLensNoise.value = clamp((Math.abs(W_POS[target] - W_POS[game.section]) - 0.6) / 1.5, 0, 0.85);
  pipeline.present();
}

function resize() {
  renderer.setSize(innerWidth, innerHeight);
  camera.aspect = innerWidth / innerHeight;
  camera.updateProjectionMatrix();
  pipeline.resize(innerWidth, innerHeight);
}
addEventListener('resize', resize);
resize();

// Console helper: advance by fixed steps without requestAnimationFrame.
window.demo.step = (seconds = 0) => {
  for (let t = 0; t < seconds; t += 1 / 60) if (game.state === 'play') update(1 / 60);
  render();
};

let last = performance.now();
function frame(now) {
  const dt = Math.min(0.05, (now - last) / 1000);
  last = now;
  if (game.state === 'play') update(dt);
  render();
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
