import * as THREE from 'three';
import { U, ps1Material, Pipeline } from './ps1.js';
import { makeTextures, ALPHA_TEXTURES, EMISSIVE_TEXTURES } from './textures.js';
import { Layer, CELL } from './level.js';
import { LAYER_DEFS, SPAWN, WARDROBE } from './world.js';
import { Creature, PERIOD } from './creature.js';
import { Sound } from './audio.js';
import { NOTES, ENDING } from './story.js';

const $ = (id) => document.getElementById(id);
const ROMAN = ['Ⅰ', 'Ⅱ', 'Ⅲ'];
const EYE = 1.58;
const RADIUS = 0.22;
const WALK = 1.45;
const RUN = 2.7;
const FLIP_T = 0.8;
const MIRROR_DRAIN = 0.4; // body per second while mirrored
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const approach = (v, t, s) => (v < t ? Math.min(t, v + s) : Math.max(t, v - s));
const modW = (w) => ((w % PERIOD) + PERIOD) % PERIOD;

const renderer = new THREE.WebGLRenderer({ antialias: false });
renderer.setPixelRatio(1);
$('app').appendChild(renderer.domElement);
const pipeline = new Pipeline(renderer);
const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(70, 1, 0.05, 30);
camera.rotation.order = 'YXZ';

const textures = makeTextures();
const materials = new Map();
function material(tex) {
  if (!materials.has(tex)) {
    materials.set(tex, ps1Material(textures[tex], {
      emissive: EMISSIVE_TEXTURES.has(tex) ? 1 : 0,
      alphaTest: ALPHA_TEXTURES.has(tex) ? 0.5 : 0,
    }));
  }
  return materials.get(tex);
}
const layers = LAYER_DEFS.map((def, i) => {
  const L = new Layer(i, def, material);
  L.build();
  scene.add(L.group);
  return L;
});
const creature = new Creature(scene);
const sound = new Sound();

const game = {
  state: 'title',
  x: SPAWN.x,
  z: SPAWN.z,
  yaw: SPAWN.yaw,
  pitch: 0,
  layer: 0,
  mirrored: false,
  body: 100,
  hasScope: false,
  peek: 0,
  peekHeld: false,
  dial: 1,
  flip: null,
  hidden: false,
  moving: false,
  running: false,
  stepPhase: 0,
  damage: 0,
  catchCooldown: 0,
  prox: 0,
  flicker: 1,
  flickerTarget: 1,
  flickerTimer: 0,
  fade: 0,
  script: null,
  seen: new Set(),
  focus: null,
  noteClose: null,
  msgTimer: 0,
  hintTimer: 0,
};
// Exposed for poking at the demo from the browser console.
window.demo = { game, creature, layers };

const peekTarget = () => (game.layer + game.dial + PERIOD) % PERIOD;

// ---------- UI ----------

function message(text, dur = 3) {
  $('msg').textContent = text;
  $('msg').classList.add('show');
  game.msgTimer = dur;
}

function hint(text, dur = 6) {
  $('hint').textContent = text;
  $('hint').classList.add('show');
  game.hintTimer = dur;
}

function setMirrored(m) {
  game.mirrored = m;
  $('msg').classList.toggle('mirror', m);
}

function openNote(id, onClose) {
  const n = NOTES[id];
  const paper = $('note').querySelector('.paper');
  $('note-text').textContent = n.text;
  paper.classList.toggle('crayon', !!n.crayon);
  // A reversed note looks normal only to a reversed reader.
  paper.classList.toggle('mirror', !!n.mirrorWritten !== game.mirrored);
  $('note').classList.remove('hidden');
  game.state = 'note';
  game.peekHeld = false;
  game.noteClose = onClose ?? null;
  sound.paper();
}

function closeNote() {
  $('note').classList.add('hidden');
  game.state = 'play';
  sound.paper();
  const cb = game.noteClose;
  game.noteClose = null;
  cb?.();
  // Esc while reading released the mouse; ask for a click before play resumes.
  if (game.pauseAfterNote) {
    game.pauseAfterNote = false;
    if (game.state === 'play') pause();
  }
}

function endScreen(text) {
  $('end-text').textContent = text;
  $('end').classList.remove('hidden');
  document.exitPointerLock?.();
}

function die(text) {
  game.state = 'dead';
  game.fade = 1;
  endScreen(text);
}

// ---------- input ----------

const keys = new Set();
const canvas = renderer.domElement;
const lock = () => canvas.requestPointerLock()?.catch?.(() => {});

addEventListener('keydown', (e) => {
  keys.add(e.code);
  if (e.code === 'KeyE') onUse();
  if (e.code === 'KeyQ') turnDial();
  if (e.code === 'KeyR' && (game.state === 'dead' || game.state === 'end')) location.reload();
});
addEventListener('keyup', (e) => keys.delete(e.code));
addEventListener('contextmenu', (e) => e.preventDefault());

addEventListener('mousedown', (e) => {
  if (game.state === 'title') return start();
  if (game.state === 'pause') return lock();
  if (game.state === 'note') {
    if (e.button === 0) closeNote();
    return;
  }
  if (game.state !== 'play') return;
  if (e.button === 2) game.peekHeld = true;
  if (e.button === 0 && game.peek > 0.8) tryFlip();
});
addEventListener('mouseup', (e) => {
  if (e.button === 2) game.peekHeld = false;
});
// Trackpads fire a burst of wheel events per gesture; take one turn per burst.
let lastWheel = 0;
addEventListener('wheel', (e) => {
  if (e.timeStamp - lastWheel > 200) turnDial();
  lastWheel = e.timeStamp;
});

addEventListener('mousemove', (e) => {
  if (game.state !== 'play' || document.pointerLockElement !== canvas) return;
  // A mirrored player sees a mirrored image, so turning has to be reversed to feel natural.
  const s = game.mirrored ? -1 : 1;
  game.yaw -= e.movementX * 0.0022 * s;
  game.pitch = clamp(game.pitch - e.movementY * 0.0022, -1.4, 1.4);
  if (game.hidden) {
    game.yaw = clamp(game.yaw, -Math.PI / 2 - 0.6, -Math.PI / 2 + 0.6);
    game.pitch = clamp(game.pitch, -0.45, 0.45);
  }
});

document.addEventListener('pointerlockchange', () => {
  const locked = document.pointerLockElement === canvas;
  if (!locked && game.state === 'play') pause();
  else if (!locked && game.state === 'note') game.pauseAfterNote = true;
  else if (locked && game.state === 'pause') {
    game.state = 'play';
    $('pause').classList.add('hidden');
  }
});

function pause() {
  game.state = 'pause';
  game.peekHeld = false;
  $('pause').classList.remove('hidden');
}

function start() {
  sound.init();
  $('title').classList.add('hidden');
  game.state = 'play';
  lock();
  message('小满在反锁的房间里消失了。', 5);
}

function turnDial() {
  if (!game.hasScope || game.state !== 'play') return;
  game.dial = -game.dial;
  sound.burst({ dur: 0.03, type: 'highpass', freq: 4000, gain: 0.12 });
}

// ---------- actions ----------

function take(it) {
  it.gone = true;
  if (it.group) it.group.visible = false;
}

function onUse() {
  if (game.state === 'note') return closeNote();
  if (game.state !== 'play' || game.flip || game.script?.lock) return;
  if (game.hidden) return unhide();
  const it = game.focus;
  if (!it) return;
  if (it.note) return openNote(it.note, it.id === 'final' ? startEnding : null);
  if (it.kind === 'scope') {
    take(it);
    game.hasScope = true;
    creature.wake();
    return openNote('scope', () => hint('按住 右键 举起立体镜　滚轮 / Q 选择要看的那一页\n举着立体镜时 左键 翻过去', 12));
  }
  if (it.kind === 'food') {
    take(it);
    sound.eat();
    if (game.mirrored) return message('你吃了下去，\n却像什么都没吃。', 3.5);
    game.body = Math.min(100, game.body + 40);
    return message('冷的。你吃完了。', 3);
  }
  if (it.kind === 'hide') return hide();
  if (it.kind === 'mirror') return message(game.mirrored ? '镜子里的你，是正的。' : '镜子是黑的。\n里面没有你。', 3.5);
  if (it.text) message(it.text, 3.5);
}

function tryFlip() {
  if (game.flip || !game.hasScope || game.hidden || game.script?.lock) return;
  const target = peekTarget();
  if (layers[target].blocked(game.x, game.z, RADIUS)) {
    message('那一页的这里是实心的。', 2.5);
    sound.blocked();
    creature.attention = Math.min(1, creature.attention + 0.1);
    return;
  }
  game.flip = { t: 0, target, swapped: false };
  game.peekHeld = false;
  sound.flip();
}

function hide() {
  game.hidden = true;
  game.x = WARDROBE.x;
  game.z = WARDROBE.z;
  game.yaw = -Math.PI / 2;
  game.pitch = 0;
  sound.burst({ dur: 0.4, type: 'lowpass', freq: 400, gain: 0.3 });
  if (!game.seen.has('wardrobe')) {
    game.seen.add('wardrobe');
    game.script = { kind: 'wardrobe', t: 0 };
  }
}

function unhide() {
  if (game.script?.kind === 'wardrobe') return;
  game.hidden = false;
  game.x = WARDROBE.exitX;
  game.z = WARDROBE.z;
  sound.burst({ dur: 0.4, type: 'lowpass', freq: 400, gain: 0.3 });
}

function caught() {
  game.body -= 34;
  game.damage = 1;
  game.catchCooldown = 4;
  setMirrored(!game.mirrored);
  sound.caught();
  creature.retreat(game.layer);
  if (game.body <= 0) return die('它把你翻了过去。\n这一次，没有再翻回来。');
  message('它碰到了你。\n你被翻了过来。', 3.5);
}

// Fingertips come out of the dark right in front of the player's face.
function startEnding() {
  game.script = { kind: 'ending', t: 0, lock: true };
  const fx = -Math.sin(game.yaw);
  const fz = -Math.cos(game.yaw);
  creature.beginScript(game.x - fx * 0.7, 1.6, game.z - fz * 0.7, Math.atan2(fx, fz), modW(game.layer - 1.4), { x: game.x, y: EYE, z: game.z });
}

// ---------- update ----------

function updateScript(dt) {
  const S = game.script;
  if (!S) return;
  S.t += dt;
  if (S.kind === 'wardrobe') {
    if (S.t > 2.5 && !S.started) {
      S.started = true;
      // Palm behind the wardrobe's back wall, facing the doors; only the fingertips cross this page.
      creature.beginScript(game.x - 0.95, 1.6, game.z, Math.PI / 2, modW(game.layer - 1.35), { x: game.x, y: EYE, z: game.z });
    }
    if (S.started) {
      const k = S.t - 2.5;
      const reach = k < 3 ? k / 3 : k < 6 ? 1 : Math.max(0, 1 - (k - 6) / 2);
      creature.w = modW(game.layer - 1.35 + 0.42 * reach * reach * (3 - 2 * reach));
      if (k > 8.5) {
        creature.endScript(game.layer);
        game.script = null;
        message('柜门一直是关着的。', 4);
      }
    }
  } else if (S.kind === 'ending') {
    const reach = clamp(S.t / 3, 0, 1);
    creature.w = modW(game.layer - 1.4 + 0.47 * reach);
    if (S.t > 3) game.flickerTarget = 0;
    if (S.t > 4.5) game.fade = clamp((S.t - 4.5) / 2, 0, 1);
    if (S.t > 7 && game.state === 'play') {
      game.state = 'end';
      endScreen(ENDING);
    }
  }
}

function updateFlip(dt) {
  const F = game.flip;
  if (!F) return;
  F.t += dt;
  if (!F.swapped && F.t >= FLIP_T / 2) {
    F.swapped = true;
    game.layer = F.target;
    setMirrored(!game.mirrored);
    sound.setLayer(game.layer);
    if (!game.seen.has('flip')) {
      game.seen.add('flip');
      message('你翻到了另一页。\n现在，你是反的。', 4);
      hint('镜像时体力会一直下降，吃东西也没有用。\n再翻一次，就能正回来。', 10);
    }
  }
  if (F.t >= FLIP_T) game.flip = null;
}

function updateMove(dt) {
  game.moving = false;
  game.running = false;
  if (game.hidden || game.flip || game.script?.lock) return;
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
  const speed = (running ? RUN : WALK) * (game.peek > 0.5 ? 0.45 : 1);
  game.moving = true;
  game.running = running;
  const fx = -Math.sin(game.yaw);
  const fz = -Math.cos(game.yaw);
  const rx = Math.cos(game.yaw);
  const rz = -Math.sin(game.yaw);
  const dx = ((fx * f + rx * s) / len) * speed * dt;
  const dz = ((fz * f + rz * s) / len) * speed * dt;
  const L = layers[game.layer];
  if (!L.blocked(game.x + dx, game.z, RADIUS)) game.x += dx;
  if (!L.blocked(game.x, game.z + dz, RADIUS)) game.z += dz;
  const before = game.stepPhase;
  game.stepPhase += speed * dt * 2.2;
  if (Math.floor(before / Math.PI) !== Math.floor(game.stepPhase / Math.PI)) {
    sound.step(running, L.cell(Math.floor(game.x / CELL), Math.floor(game.z / CELL)));
  }
}

function updatePeek(dt) {
  const want = game.peekHeld && game.hasScope && !game.flip && !game.script?.lock;
  if (want !== game.peekOn) {
    game.peekOn = want;
    sound.peek(want);
  }
  game.peek = approach(game.peek, want ? 1 : 0, dt * 4);
}

const camDir = new THREE.Vector3();

function updateCreature(dt) {
  camera.getWorldDirection(camDir);
  const ev = creature.update(dt, {
    x: game.x,
    z: game.z,
    layer: game.layer,
    moving: game.moving,
    running: game.running,
    peeking: game.peek > 0.8,
    peekLayer: peekTarget(),
    camPos: camera.position,
    camDir,
  });
  if (ev === 'hunt') sound.sting();
  game.prox = creature.state === 'dormant' ? 0 : creature.proximity(game.x, EYE, game.z, game.layer);
  if (game.catchCooldown > 0 || game.flip || !creature.touches(game.x, game.z, game.layer)) return;
  if (creature.state === 'hunt') caught();
  else if (creature.state === 'wander') {
    // Brushing past an unaware hand gets you noticed, not caught: a beat to react.
    creature.notice();
    sound.sting();
    game.catchCooldown = 1.5;
  }
}

function updateFlicker(dt) {
  game.flickerTimer -= dt;
  if (game.script?.kind === 'ending' && game.script.t > 3) {
    game.flicker = 0;
    return;
  }
  if (game.flickerTimer <= 0) {
    const scripted = game.script ? 0.5 : 0;
    if (Math.random() < game.prox * game.prox * 0.9 + scripted + 0.01) {
      game.flickerTarget = 0.1 + Math.random() * 0.45;
      game.flickerTimer = 0.04 + Math.random() * 0.12;
    } else {
      game.flickerTarget = 1;
      game.flickerTimer = 0.1 + Math.random() * 0.4;
    }
  }
  game.flicker = game.flickerTarget;
}

const toItem = new THREE.Vector3();

function updateInteract() {
  game.focus = null;
  let text = '';
  if (game.hidden && !game.script) text = '[E] 出来';
  else if (!game.flip && game.peek < 0.3 && !game.script?.lock && !game.hidden) {
    camera.getWorldDirection(camDir);
    let bestDot = 0.8;
    for (const it of layers[game.layer].items) {
      if (it.gone) continue;
      toItem.set(it.x - camera.position.x, it.y - camera.position.y, it.z - camera.position.z);
      if (toItem.length() > 1.9) continue;
      const dot = toItem.normalize().dot(camDir);
      if (dot > bestDot) {
        bestDot = dot;
        game.focus = it;
      }
    }
    if (game.focus) text = `[E] ${game.focus.prompt}`;
  }
  $('prompt').textContent = text;
}

function updateBody(dt) {
  if (game.mirrored && !game.script) game.body -= dt * MIRROR_DRAIN;
  game.damage = Math.max(0, game.damage - dt * 0.7);
  game.catchCooldown -= dt;
  if (game.body <= 0) die('你越来越轻。\n最后，轻得像一页纸。');
}

function updateHud(dt) {
  game.msgTimer -= dt;
  if (game.msgTimer <= 0) $('msg').classList.remove('show');
  game.hintTimer -= dt;
  if (game.hintTimer <= 0) $('hint').classList.remove('show');
  $('page').textContent = `第 ${ROMAN[game.layer]} 页`;
  $('side').textContent = game.mirrored ? '反' : '正';
  $('status').classList.toggle('mirrored', game.mirrored);
  $('bar').firstElementChild.style.width = `${clamp(game.body, 0, 100)}%`;
  $('dial').classList.toggle('on', game.peek > 0.5);
  const tgt = peekTarget();
  const solid = layers[tgt].blocked(game.x, game.z, RADIUS);
  for (const span of $('dial').children) {
    const l = Number(span.dataset.l);
    const p = creature.presence(l);
    span.className = [l === game.layer ? 'cur' : '', l === tgt ? 'tgt' : '', l === tgt && solid ? 'solid' : '', p].join(' ');
  }
}

function update(dt) {
  U.uTime.value += dt;
  updateFlip(dt);
  updateMove(dt);
  updatePeek(dt);
  updateScript(dt);
  updateCreature(dt);
  updateFlicker(dt);
  updateInteract();
  updateBody(dt);
  updateHud(dt);
  const danger = Math.max(game.prox, creature.state === 'hunt' ? 0.7 : 0, game.body < 30 ? 0.6 : 0);
  sound.update(dt, game.prox, danger);
}

// ---------- render ----------

function applyLayer(i) {
  const d = layers[i].def;
  U.uFogColor.value.setRGB(...d.fog);
  U.uFogNear.value = d.fogNear;
  U.uFogFar.value = d.fogFar;
  U.uAmbient.value.setRGB(...d.ambient);
  for (const L of layers) L.group.visible = L.index === i;
}

function render() {
  const bob = game.moving ? Math.sin(game.stepPhase) * 0.035 : 0;
  camera.position.set(game.x, EYE + bob + (game.hidden ? -0.05 : 0), game.z);
  camera.rotation.set(game.pitch, game.yaw, 0);
  camera.updateMatrixWorld();
  U.uLightPos.value.set(game.x, EYE - 0.25, game.z);
  U.uLightRange.value = game.hidden ? 1.6 : 4.2;
  U.uGlobal.value = game.flicker;

  creature.slice(0, game.layer);
  creature.meshes[0].visible = true;
  creature.meshes[1].visible = false;
  applyLayer(game.layer);
  pipeline.renderTo(pipeline.rtMain, scene, camera, U.uFogColor.value);

  if (game.peek > 0.01) {
    const t = peekTarget();
    creature.slice(1, t);
    creature.meshes[0].visible = false;
    creature.meshes[1].visible = true;
    applyLayer(t);
    pipeline.renderTo(pipeline.rtPeek, scene, camera, U.uFogColor.value);
  }

  const P = pipeline.post.uniforms;
  const F = game.flip;
  P.uPeek.value = game.peek;
  P.uMirror.value = game.mirrored ? 1 : 0;
  P.uSqueeze.value = F ? Math.max(0.002, Math.abs(Math.cos((Math.PI * F.t) / FLIP_T))) : 1;
  P.uTime.value = U.uTime.value;
  P.uNoise.value = game.prox * 0.6 + (F ? 0.8 : 0) + game.damage * 0.5;
  P.uWarp.value = game.prox + (F ? 2 : 0);
  P.uDamage.value = game.damage;
  P.uDesat.value = clamp((50 - game.body) / 60, 0, 0.8);
  P.uFade.value = game.fade;
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

// Console helper: advance the game by fixed steps without relying on requestAnimationFrame.
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
