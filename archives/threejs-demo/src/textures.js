import * as THREE from 'three';

// All textures are painted at PS1-scale sizes on a canvas and sampled with nearest filtering.

export function rng(seed) {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

export function canvas(w, h) {
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  return [c, c.getContext('2d')];
}

export function grain(ctx, w, h, r, amount) {
  const img = ctx.getImageData(0, 0, w, h);
  const d = img.data;
  for (let i = 0; i < d.length; i += 4) {
    const n = (r() - 0.5) * amount;
    d[i] += n;
    d[i + 1] += n;
    d[i + 2] += n;
  }
  ctx.putImageData(img, 0, 0);
}

export function blotches(ctx, w, h, r, count, color, maxR) {
  ctx.fillStyle = color;
  for (let i = 0; i < count; i++) {
    ctx.beginPath();
    ctx.ellipse(r() * w, r() * h, 1 + r() * maxR, 1 + r() * maxR * 0.7, r() * 3, 0, Math.PI * 2);
    ctx.fill();
  }
}

export function toTex(c) {
  const t = new THREE.CanvasTexture(c);
  t.magFilter = THREE.NearestFilter;
  t.minFilter = THREE.NearestFilter;
  t.generateMipmaps = false;
  t.wrapS = t.wrapT = THREE.RepeatWrapping;
  return t;
}

const FONT = '"Microsoft YaHei", "SimHei", sans-serif';
const KAI = '"KaiTi", "STKaiti", serif';

// Painted wall: plaster above, a green wainscot below the 1.2 m line (bottom 43% of the texture).
function wall(variant) {
  const P = [
    { top: '#c9bfa6', low: '#3f6a4e', line: '#24402e', skirt: '#2c3229' },
    { top: '#8e959b', low: '#4a565e', line: '#2e383f', skirt: '#2a2e31' },
    { top: '#7a4a3e', low: '#5a2a20', line: '#2a0c08', skirt: '#1a0806' },
  ][variant];
  const [c, x] = canvas(64, 128);
  const r = rng(11 + variant);
  x.fillStyle = P.top;
  x.fillRect(0, 0, 64, 128);
  x.fillStyle = P.low;
  x.fillRect(0, 73, 64, 55);
  x.fillStyle = P.line;
  x.fillRect(0, 71, 64, 2);
  x.fillStyle = P.skirt;
  x.fillRect(0, 124, 64, 4);
  blotches(x, 64, 70, r, 6, 'rgba(70,55,35,0.12)', 9);
  blotches(x, 64, 128, r, 5, 'rgba(0,0,0,0.12)', 6);
  if (variant === 1) blotches(x, 64, 128, r, 10, 'rgba(200,205,210,0.18)', 6);
  // Drips running down from the ceiling.
  for (let i = 0; i < (variant === 2 ? 9 : 3); i++) {
    const dx = Math.floor(r() * 64);
    x.fillStyle = variant === 2 ? 'rgba(20,0,0,0.5)' : 'rgba(90,70,40,0.18)';
    x.fillRect(dx, 0, 1 + Math.floor(r() * 2), 10 + r() * 50);
  }
  grain(x, 64, 128, r, 14);
  return toTex(c);
}

function flesh() {
  const [c, x] = canvas(64, 64);
  const r = rng(7);
  x.fillStyle = '#6e2a22';
  x.fillRect(0, 0, 64, 64);
  blotches(x, 64, 64, r, 30, 'rgba(150,60,45,0.35)', 7);
  blotches(x, 64, 64, r, 20, 'rgba(20,0,0,0.4)', 5);
  x.strokeStyle = 'rgba(140,60,50,0.55)';
  for (let i = 0; i < 6; i++) {
    let px = r() * 64;
    let py = r() * 64;
    x.beginPath();
    x.moveTo(px, py);
    for (let k = 0; k < 8; k++) {
      px += (r() - 0.5) * 16;
      py += (r() - 0.3) * 12;
      x.lineTo(px, py);
    }
    x.stroke();
  }
  grain(x, 64, 64, r, 18);
  return toTex(c);
}

function terrazzo(variant) {
  const base = ['#a39c8b', '#868c92', '#3b2622'][variant];
  const [c, x] = canvas(64, 64);
  const r = rng(21 + variant);
  x.fillStyle = base;
  x.fillRect(0, 0, 64, 64);
  const chips = variant === 2
    ? ['#5a3a33', '#20100d', '#6a2a22']
    : ['#5d5a52', '#e4ded0', '#8a5a48', '#4f6a58', '#2e2c28'];
  for (let i = 0; i < 260; i++) {
    x.fillStyle = chips[Math.floor(r() * chips.length)];
    x.fillRect(Math.floor(r() * 64), Math.floor(r() * 64), 1 + Math.floor(r() * 2), 1 + Math.floor(r() * 2));
  }
  x.fillStyle = variant === 2 ? '#1a0c0a' : '#6b6452';
  x.fillRect(0, 0, 64, 1);
  x.fillRect(0, 0, 1, 64);
  blotches(x, 64, 64, r, 4, 'rgba(0,0,0,0.1)', 12);
  grain(x, 64, 64, r, 10);
  return toTex(c);
}

function woodFloor(variant) {
  const [c, x] = canvas(64, 64);
  const r = rng(31 + variant);
  const cols = [['#6a2e21', '#7c3a2a'], ['#5c4a44', '#665550'], ['#33160f', '#3d1c14']][variant];
  for (let i = 0; i < 4; i++) {
    x.fillStyle = cols[i % 2];
    x.fillRect(0, i * 16, 64, 16);
    x.fillStyle = 'rgba(0,0,0,0.5)';
    x.fillRect(0, i * 16, 64, 1);
    const seam = Math.floor(r() * 64);
    x.fillRect(seam, i * 16, 1, 16);
    x.fillStyle = 'rgba(0,0,0,0.12)';
    for (let k = 0; k < 4; k++) x.fillRect(0, i * 16 + 3 + Math.floor(r() * 11), 64, 1);
  }
  blotches(x, 64, 64, r, 6, 'rgba(160,110,80,0.15)', 10);
  grain(x, 64, 64, r, 12);
  return toTex(c);
}

function tiles(variant) {
  const [c, x] = canvas(64, 64);
  const r = rng(41 + variant);
  x.fillStyle = ['#8a877c', '#6f747a', '#2a1210'][variant];
  x.fillRect(0, 0, 64, 64);
  const face = ['#d6d2c6', '#aeb3b8', '#4a2520'][variant];
  for (let i = 0; i < 8; i++) {
    for (let j = 0; j < 8; j++) {
      x.fillStyle = face;
      x.fillRect(i * 8 + 1, j * 8 + 1, 7, 7);
    }
  }
  blotches(x, 64, 64, r, 5, 'rgba(80,60,30,0.15)', 10);
  grain(x, 64, 64, r, 10);
  return toTex(c);
}

function ceiling(variant) {
  const [c, x] = canvas(64, 64);
  const r = rng(51 + variant);
  x.fillStyle = ['#bab19c', '#8b9094', '#3a1d18'][variant];
  x.fillRect(0, 0, 64, 64);
  x.strokeStyle = variant === 2 ? 'rgba(10,0,0,0.5)' : 'rgba(120,90,40,0.3)';
  for (let i = 0; i < 3; i++) {
    x.beginPath();
    x.ellipse(r() * 64, r() * 64, 6 + r() * 12, 5 + r() * 9, r() * 3, 0, Math.PI * 2);
    x.stroke();
  }
  grain(x, 64, 64, r, 10);
  return toTex(c);
}

function door(color, withFu) {
  const [c, x] = canvas(64, 128);
  const r = rng(61);
  x.fillStyle = color;
  x.fillRect(0, 0, 64, 128);
  x.strokeStyle = 'rgba(0,0,0,0.45)';
  x.lineWidth = 2;
  x.strokeRect(8, 10, 48, 44);
  x.strokeRect(8, 66, 48, 52);
  x.fillStyle = 'rgba(0,0,0,0.6)';
  x.fillRect(0, 0, 2, 128);
  x.fillRect(62, 0, 2, 128);
  x.fillStyle = '#b8a060';
  x.fillRect(52, 60, 5, 5);
  if (withFu) {
    // The upside-down 福 pasted on Chinese front doors.
    x.save();
    x.translate(32, 32);
    x.rotate(Math.PI / 4);
    x.fillStyle = '#8e1f16';
    x.fillRect(-13, -13, 26, 26);
    x.restore();
    x.save();
    x.translate(32, 32);
    x.rotate(Math.PI);
    x.fillStyle = '#c9a54a';
    x.font = `bold 20px ${FONT}`;
    x.textAlign = 'center';
    x.textBaseline = 'middle';
    x.fillText('福', 0, 1);
    x.restore();
    x.fillStyle = '#111';
    x.fillRect(31, 46, 3, 3);
  }
  blotches(x, 64, 128, r, 8, 'rgba(0,0,0,0.15)', 6);
  grain(x, 64, 128, r, 14);
  return toTex(c);
}

function wood(base) {
  const [c, x] = canvas(64, 64);
  const r = rng(71);
  x.fillStyle = base;
  x.fillRect(0, 0, 64, 64);
  x.fillStyle = 'rgba(0,0,0,0.15)';
  for (let i = 0; i < 18; i++) x.fillRect(0, Math.floor(r() * 64), 64, 1);
  x.strokeStyle = 'rgba(0,0,0,0.5)';
  x.strokeRect(0.5, 0.5, 63, 63);
  grain(x, 64, 64, r, 12);
  return toTex(c);
}

function plaid(a, b) {
  const [c, x] = canvas(64, 64);
  const r = rng(81);
  x.fillStyle = a;
  x.fillRect(0, 0, 64, 64);
  x.fillStyle = b;
  for (let i = 0; i < 64; i += 16) {
    x.fillRect(i, 0, 6, 64);
    x.fillRect(0, i, 64, 6);
  }
  x.fillStyle = 'rgba(255,255,255,0.15)';
  for (let i = 8; i < 64; i += 16) {
    x.fillRect(i, 0, 1, 64);
    x.fillRect(0, i, 64, 1);
  }
  grain(x, 64, 64, r, 14);
  return toTex(c);
}

function sheet() {
  const [c, x] = canvas(64, 64);
  const r = rng(91);
  x.fillStyle = '#bdbab2';
  x.fillRect(0, 0, 64, 64);
  for (let i = 0; i < 7; i++) {
    const g = x.createLinearGradient(0, 0, 10, 0);
    g.addColorStop(0, 'rgba(0,0,0,0)');
    g.addColorStop(0.5, 'rgba(0,0,0,0.18)');
    g.addColorStop(1, 'rgba(0,0,0,0)');
    x.save();
    x.translate(r() * 64, 0);
    x.fillStyle = g;
    x.fillRect(0, 0, 10, 64);
    x.restore();
  }
  blotches(x, 64, 64, r, 4, 'rgba(90,80,50,0.15)', 8);
  grain(x, 64, 64, r, 10);
  return toTex(c);
}

function cardboard() {
  const [c, x] = canvas(32, 32);
  const r = rng(101);
  x.fillStyle = '#8f7048';
  x.fillRect(0, 0, 32, 32);
  x.fillStyle = '#b8a77a';
  x.fillRect(0, 14, 32, 4);
  x.strokeStyle = 'rgba(0,0,0,0.4)';
  x.strokeRect(0.5, 0.5, 31, 31);
  x.fillStyle = 'rgba(30,20,10,0.6)';
  x.fillRect(5, 22, 10, 1);
  x.fillRect(5, 25, 6, 1);
  grain(x, 32, 32, r, 14);
  return toTex(c);
}

function enamel(base) {
  const [c, x] = canvas(32, 32);
  const r = rng(111);
  x.fillStyle = base;
  x.fillRect(0, 0, 32, 32);
  blotches(x, 32, 32, r, 5, 'rgba(40,40,40,0.5)', 2);
  blotches(x, 32, 32, r, 3, 'rgba(120,90,40,0.2)', 6);
  grain(x, 32, 32, r, 8);
  return toTex(c);
}

function solid(color) {
  const [c, x] = canvas(8, 8);
  x.fillStyle = color;
  x.fillRect(0, 0, 8, 8);
  return toTex(c);
}

function crt() {
  const [c, x] = canvas(32, 32);
  const r = rng(121);
  x.fillStyle = '#3a3833';
  x.fillRect(0, 0, 32, 32);
  x.fillStyle = '#18201b';
  x.fillRect(3, 3, 26, 22);
  x.fillStyle = 'rgba(255,255,255,0.12)';
  x.fillRect(5, 5, 8, 3);
  for (let i = 4; i < 25; i += 2) {
    x.fillStyle = 'rgba(0,0,0,0.25)';
    x.fillRect(3, i, 26, 1);
  }
  x.fillStyle = '#222';
  x.fillRect(22, 27, 3, 3);
  x.fillRect(26, 27, 3, 3);
  grain(x, 32, 32, r, 10);
  return toTex(c);
}

function calendar() {
  const [c, x] = canvas(32, 48);
  x.fillStyle = '#d9d3c3';
  x.fillRect(0, 0, 32, 48);
  x.fillStyle = '#9a2318';
  x.fillRect(0, 0, 32, 11);
  x.fillStyle = '#e8d9a8';
  x.font = `bold 9px ${FONT}`;
  x.textAlign = 'center';
  x.fillText('1997', 16, 9);
  x.fillStyle = '#9a2318';
  x.font = `bold 20px ${FONT}`;
  x.fillText('12', 16, 32);
  x.fillStyle = '#333';
  x.font = `8px ${FONT}`;
  x.fillText('三月', 16, 43);
  return toTex(c);
}

function fu() {
  const [c, x] = canvas(32, 32);
  x.clearRect(0, 0, 32, 32);
  x.save();
  x.translate(16, 16);
  x.rotate(Math.PI / 4);
  x.fillStyle = '#9a2318';
  x.fillRect(-11, -11, 22, 22);
  x.restore();
  x.fillStyle = '#d4b04c';
  x.font = `bold 17px ${FONT}`;
  x.textAlign = 'center';
  x.textBaseline = 'middle';
  x.fillText('福', 16, 17);
  return toTex(c);
}

function windowTex() {
  const [c, x] = canvas(64, 64);
  const r = rng(131);
  x.fillStyle = '#4a3a2a';
  x.fillRect(0, 0, 64, 64);
  x.fillStyle = '#0b0f16';
  x.fillRect(5, 5, 54, 54);
  x.fillStyle = 'rgba(120,140,170,0.12)';
  x.fillRect(8, 8, 14, 20);
  x.fillStyle = '#4a3a2a';
  x.fillRect(30, 5, 4, 54);
  x.fillRect(5, 30, 54, 3);
  // Welded security bars.
  x.fillStyle = '#5b5e60';
  for (let i = 10; i < 60; i += 9) x.fillRect(i, 0, 2, 64);
  x.fillRect(0, 18, 64, 2);
  x.fillRect(0, 46, 64, 2);
  grain(x, 64, 64, r, 10);
  return toTex(c);
}

function mirror() {
  const [c, x] = canvas(32, 32);
  x.fillStyle = '#6a6458';
  x.fillRect(0, 0, 32, 32);
  x.fillStyle = '#05070a';
  x.fillRect(2, 2, 28, 28);
  x.fillStyle = 'rgba(160,170,180,0.1)';
  x.fillRect(4, 4, 6, 10);
  return toTex(c);
}

function clock() {
  const [c, x] = canvas(32, 32);
  x.clearRect(0, 0, 32, 32);
  x.fillStyle = '#3a2a1a';
  x.beginPath();
  x.arc(16, 16, 15, 0, Math.PI * 2);
  x.fill();
  x.fillStyle = '#d8d0b8';
  x.beginPath();
  x.arc(16, 16, 12, 0, Math.PI * 2);
  x.fill();
  x.strokeStyle = '#111';
  x.lineWidth = 2;
  x.beginPath();
  x.moveTo(16, 16);
  x.lineTo(16, 7);
  x.moveTo(16, 16);
  x.lineTo(22, 19);
  x.stroke();
  return toTex(c);
}

function drawing(seed) {
  const [c, x] = canvas(48, 40);
  const r = rng(141 + seed);
  x.fillStyle = '#ddd6c4';
  x.fillRect(0, 0, 48, 40);
  x.lineWidth = 1.5;
  x.strokeStyle = '#3550a0';
  x.strokeRect(8, 18, 16, 14);
  x.beginPath();
  x.moveTo(6, 18);
  x.lineTo(16, 9);
  x.lineTo(26, 18);
  x.stroke();
  x.strokeStyle = '#222';
  x.beginPath();
  x.arc(34, 22, 2, 0, Math.PI * 2);
  x.moveTo(34, 24);
  x.lineTo(34, 31);
  x.moveTo(34, 31);
  x.lineTo(31, 36);
  x.moveTo(34, 31);
  x.lineTo(37, 36);
  x.stroke();
  // Five floating circles: the hand as she saw it.
  x.fillStyle = '#9a2b1e';
  for (let i = 0; i < 5; i++) {
    x.beginPath();
    x.arc(28 + i * 4 + r() * 2, 6 + r() * 6, 2 + r() * 1.5, 0, Math.PI * 2);
    x.fill();
  }
  return toTex(c);
}

function writing() {
  // Crayon text written mirror-reversed: readable only when the viewer is mirrored too.
  const [c, x] = canvas(128, 48);
  x.clearRect(0, 0, 128, 48);
  x.save();
  x.translate(128, 0);
  x.scale(-1, 1);
  x.fillStyle = '#c03a22';
  x.font = `bold 18px ${KAI}`;
  x.textAlign = 'center';
  x.fillText('爸爸', 64, 18);
  x.fillText('我在这一页', 64, 40);
  x.restore();
  return toTex(c);
}

function paper() {
  const [c, x] = canvas(16, 16);
  x.fillStyle = '#d8d0bc';
  x.fillRect(0, 0, 16, 16);
  x.fillStyle = 'rgba(40,40,60,0.5)';
  for (let i = 3; i < 15; i += 3) x.fillRect(2, i, 10 + (i % 4), 1);
  return toTex(c);
}

function bowl() {
  const [c, x] = canvas(16, 16);
  x.fillStyle = '#e0dcd0';
  x.fillRect(0, 0, 16, 16);
  x.fillStyle = '#2d4f8a';
  x.fillRect(0, 0, 16, 2);
  x.fillRect(0, 7, 16, 1);
  return toTex(c);
}

function bulb() {
  return solid('#fff3c8');
}

function tube() {
  return solid('#e6f2ee');
}

function stereoscope() {
  const [c, x] = canvas(16, 16);
  x.fillStyle = '#3a2416';
  x.fillRect(0, 0, 16, 16);
  x.fillStyle = '#0a0a0a';
  x.beginPath();
  x.arc(4.5, 8, 3, 0, Math.PI * 2);
  x.arc(11.5, 8, 3, 0, Math.PI * 2);
  x.fill();
  x.fillStyle = '#b89a50';
  x.fillRect(0, 0, 16, 1);
  return toTex(c);
}

export function makeTextures() {
  return {
    wall0: wall(0), wall1: wall(1), wall2: wall(2), flesh: flesh(),
    terrazzo0: terrazzo(0), terrazzo1: terrazzo(1), terrazzo2: terrazzo(2),
    woodfloor0: woodFloor(0), woodfloor1: woodFloor(1), woodfloor2: woodFloor(2),
    tile0: tiles(0), tile1: tiles(1), tile2: tiles(2),
    ceiling0: ceiling(0), ceiling1: ceiling(1), ceiling2: ceiling(2),
    door: door('#6b2a1e', false), frontdoor: door('#3d5245', true), door2: door('#3a120c', false),
    wood: wood('#7a5536'), darkwood: wood('#4f3322'), rotwood: wood('#2e1a12'),
    quilt: plaid('#7a4a52', '#3f5578'), sofa: plaid('#5a4a3a', '#4a3a2c'), rotcloth: plaid('#3a1c18', '#2a0e0c'),
    sheet: sheet(), cardboard: cardboard(),
    enamel: enamel('#d6d4ca'), steel: enamel('#55575a'), black: solid('#151412'), skin: solid('#7a3a30'),
    crt: crt(), calendar: calendar(), fu: fu(), window: windowTex(), mirror: mirror(), clock: clock(),
    drawing0: drawing(0), drawing1: drawing(1), drawing2: drawing(2), writing: writing(),
    paper: paper(), bowl: bowl(), rice: solid('#eeeae0'), bun: solid('#e2d8c0'),
    bulb: bulb(), tube: tube(), stereoscope: stereoscope(),
  };
}

// Textures with transparent pixels get an alpha-tested material.
export const ALPHA_TEXTURES = new Set(['fu', 'clock', 'writing']);
// Textures that light themselves.
export const EMISSIVE_TEXTURES = new Set(['bulb', 'tube']);
