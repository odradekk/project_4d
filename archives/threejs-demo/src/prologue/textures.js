import { rng, canvas, grain, blotches, toTex } from '../textures.js';

const KAI = '"KaiTi", "STKaiti", serif';
const HEI = '"Microsoft YaHei", "SimHei", sans-serif';

// Variant 0: her room. 1: the same room one page over, washed out and empty. 2: deep, too clean.
function roomWall(v) {
  const P = [
    { top: '#e4d3c8', dot: 'rgba(196,112,126,0.35)', low: '#7fae96', line: '#4f7f68', skirt: '#5a4a3e' },
    { top: '#c8cdd2', dot: 'rgba(120,130,150,0.12)', low: '#92a6a1', line: '#6d827d', skirt: '#5e6266' },
    { top: '#f4dae3', dot: 'rgba(232,150,182,0.5)', low: '#a2dcc6', line: '#80c4ab', skirt: '#ecc9d3' },
  ][v];
  const [c, x] = canvas(64, 128);
  const r = rng(201 + v);
  x.fillStyle = P.top;
  x.fillRect(0, 0, 64, 73);
  // Little printed flowers on the whitewash.
  x.fillStyle = P.dot;
  for (let j = 4; j < 70; j += 12) {
    for (let i = (j / 12) % 2 ? 2 : 8; i < 64; i += 12) x.fillRect(i, j, 2, 2);
  }
  x.fillStyle = P.low;
  x.fillRect(0, 73, 64, 55);
  x.fillStyle = P.line;
  x.fillRect(0, 71, 64, 2);
  x.fillStyle = P.skirt;
  x.fillRect(0, 124, 64, 4);
  if (v === 0) {
    blotches(x, 64, 70, r, 4, 'rgba(120,90,60,0.08)', 9);
    blotches(x, 64, 128, r, 3, 'rgba(0,0,0,0.08)', 5);
  }
  grain(x, 64, 128, r, v === 2 ? 3 : 10);
  return toTex(c);
}

function plank(v) {
  const cols = [['#8a4a33', '#7c4029'], ['#8b7c76', '#837570'], ['#c06a50', '#b8644b']][v];
  const [c, x] = canvas(64, 64);
  const r = rng(211 + v);
  for (let i = 0; i < 4; i++) {
    x.fillStyle = cols[i % 2];
    x.fillRect(0, i * 16, 64, 16);
    x.fillStyle = 'rgba(0,0,0,0.35)';
    x.fillRect(0, i * 16, 64, 1);
    x.fillRect(Math.floor(r() * 64), i * 16, 1, 16);
    if (v !== 2) {
      x.fillStyle = 'rgba(0,0,0,0.08)';
      for (let k = 0; k < 3; k++) x.fillRect(0, i * 16 + 3 + Math.floor(r() * 11), 64, 1);
    } else {
      x.fillStyle = 'rgba(255,255,255,0.18)';
      x.fillRect(0, i * 16 + 3, 64, 1);
    }
  }
  if (v === 0) blotches(x, 64, 64, r, 5, 'rgba(180,120,90,0.15)', 9);
  grain(x, 64, 64, r, v === 2 ? 3 : 10);
  return toTex(c);
}

function ceil(v) {
  const [c, x] = canvas(32, 32);
  const r = rng(221 + v);
  x.fillStyle = ['#e2ddd3', '#c8ccd1', '#f6eaef'][v];
  x.fillRect(0, 0, 32, 32);
  grain(x, 32, 32, r, v === 2 ? 2 : 8);
  return toTex(c);
}

function paint(color, seed) {
  const [c, x] = canvas(32, 32);
  const r = rng(seed);
  x.fillStyle = color;
  x.fillRect(0, 0, 32, 32);
  grain(x, 32, 32, r, 8);
  return toTex(c);
}

function door(color, sticker) {
  const [c, x] = canvas(64, 128);
  const r = rng(231);
  x.fillStyle = color;
  x.fillRect(0, 0, 64, 128);
  x.strokeStyle = 'rgba(0,0,0,0.25)';
  x.lineWidth = 2;
  x.strokeRect(8, 10, 48, 46);
  x.strokeRect(8, 66, 48, 54);
  x.fillStyle = 'rgba(0,0,0,0.4)';
  x.fillRect(0, 0, 2, 128);
  x.fillRect(62, 0, 2, 128);
  x.fillStyle = '#c9b070';
  x.fillRect(52, 62, 5, 5);
  if (sticker) {
    // A star sticker she put there herself, half peeled.
    x.fillStyle = '#e8c44a';
    x.beginPath();
    for (let i = 0; i < 10; i++) {
      const a = (i / 10) * Math.PI * 2 - Math.PI / 2;
      const rr = i % 2 ? 4 : 9;
      x.lineTo(30 + Math.cos(a) * rr, 88 + Math.sin(a) * rr);
    }
    x.fill();
  }
  grain(x, 64, 128, r, 8);
  return toTex(c);
}

function cabinetFront(color) {
  const [c, x] = canvas(64, 24);
  x.fillStyle = color;
  x.fillRect(0, 0, 64, 24);
  x.strokeStyle = 'rgba(0,0,0,0.3)';
  x.strokeRect(2.5, 2.5, 28, 19);
  x.strokeRect(33.5, 2.5, 28, 19);
  x.fillStyle = '#c9b070';
  x.fillRect(28, 11, 2, 3);
  x.fillRect(34, 11, 2, 3);
  return toTex(c);
}

function windowView(v) {
  const [c, x] = canvas(64, 64);
  const r = rng(241 + v);
  const frame = ['#e8e4dc', '#c7c9cc', '#ffffff'][v];
  if (v === 2) {
    const g = x.createLinearGradient(0, 0, 0, 64);
    g.addColorStop(0, '#f7c9b8');
    g.addColorStop(0.6, '#fbe3c4');
    g.addColorStop(1, '#f6d6dc');
    x.fillStyle = g;
  } else {
    const g = x.createLinearGradient(0, 0, 0, 64);
    g.addColorStop(0, v === 0 ? '#0c1630' : '#141a24');
    g.addColorStop(1, v === 0 ? '#1d2440' : '#1c2028');
    x.fillStyle = g;
  }
  x.fillRect(0, 0, 64, 64);
  // The block of flats across the yard.
  x.fillStyle = ['#0a0d16', '#0f1218', '#f0d0d6'][v];
  x.fillRect(4, 30, 26, 34);
  x.fillRect(34, 24, 28, 40);
  if (v === 0) {
    x.fillStyle = '#f2d37a';
    for (let i = 0; i < 26; i++) {
      if (r() < 0.45) continue;
      const bx = r() < 0.5 ? 6 + Math.floor(r() * 5) * 5 : 36 + Math.floor(r() * 5) * 5;
      const by = (bx < 30 ? 33 : 27) + Math.floor(r() * 6) * 5;
      x.fillRect(bx, by, 2, 2);
    }
  }
  if (v === 2) {
    x.fillStyle = 'rgba(255,255,255,0.7)';
    x.beginPath();
    x.arc(48, 12, 6, 0, Math.PI * 2);
    x.fill();
  }
  x.fillStyle = frame;
  x.fillRect(0, 0, 64, 3);
  x.fillRect(0, 61, 64, 3);
  x.fillRect(0, 0, 3, 64);
  x.fillRect(61, 0, 3, 64);
  x.fillRect(31, 0, 2, 64);
  x.fillRect(0, 31, 64, 2);
  return toTex(c);
}

function curtain() {
  const [c, x] = canvas(32, 64);
  const r = rng(251);
  x.fillStyle = '#b9d3e6';
  x.fillRect(0, 0, 32, 64);
  x.fillStyle = '#f1d36a';
  for (let i = 0; i < 18; i++) {
    const px = Math.floor(r() * 30);
    const py = Math.floor(r() * 62);
    x.fillRect(px, py, 2, 2);
    x.fillStyle = r() < 0.5 ? '#f1d36a' : '#f19fae';
  }
  for (let i = 0; i < 32; i += 6) {
    x.fillStyle = 'rgba(0,0,0,0.12)';
    x.fillRect(i, 0, 2, 64);
  }
  return toTex(c);
}

function quilt() {
  const [c, x] = canvas(64, 64);
  const r = rng(261);
  x.fillStyle = '#e7a7b2';
  x.fillRect(0, 0, 64, 64);
  for (let i = 0; i < 22; i++) {
    const px = r() * 64;
    const py = r() * 64;
    x.fillStyle = '#fbeff0';
    for (let k = 0; k < 5; k++) {
      const a = (k / 5) * Math.PI * 2;
      x.fillRect(px + Math.cos(a) * 2, py + Math.sin(a) * 2, 2, 2);
    }
    x.fillStyle = '#f2c94c';
    x.fillRect(px, py, 1, 1);
  }
  grain(x, 64, 64, r, 8);
  return toTex(c);
}

function pillow() {
  const [c, x] = canvas(32, 16);
  x.fillStyle = '#f2efe8';
  x.fillRect(0, 0, 32, 16);
  x.strokeStyle = '#7fa6d6';
  x.strokeRect(1.5, 1.5, 29, 13);
  return toTex(c);
}

function wood(base, seed) {
  const [c, x] = canvas(32, 32);
  const r = rng(seed);
  x.fillStyle = base;
  x.fillRect(0, 0, 32, 32);
  x.fillStyle = 'rgba(0,0,0,0.12)';
  for (let i = 0; i < 9; i++) x.fillRect(0, Math.floor(r() * 32), 32, 1);
  grain(x, 32, 32, r, 8);
  return toTex(c);
}

function wardrobeFront() {
  const [c, x] = canvas(64, 96);
  const r = rng(271);
  x.fillStyle = '#c9a26b';
  x.fillRect(0, 0, 64, 96);
  x.strokeStyle = 'rgba(60,35,10,0.5)';
  x.strokeRect(2.5, 2.5, 28, 91);
  x.strokeRect(33.5, 2.5, 28, 91);
  // The full-length mirror on the right door.
  x.fillStyle = '#26303a';
  x.beginPath();
  x.ellipse(47.5, 44, 10, 26, 0, 0, Math.PI * 2);
  x.fill();
  x.fillStyle = 'rgba(200,215,230,0.25)';
  x.fillRect(42, 26, 3, 16);
  x.fillStyle = '#8a6a3a';
  x.fillRect(29, 46, 2, 6);
  x.fillRect(34, 46, 2, 6);
  grain(x, 64, 96, r, 8);
  return toTex(c);
}

function award(blank) {
  const [c, x] = canvas(64, 48);
  x.fillStyle = '#f3e7cf';
  x.fillRect(0, 0, 64, 48);
  x.strokeStyle = '#b8342a';
  x.lineWidth = 3;
  x.strokeRect(2, 2, 60, 44);
  x.strokeStyle = '#d6a845';
  x.lineWidth = 1;
  x.strokeRect(5.5, 5.5, 53, 37);
  x.fillStyle = '#b8342a';
  x.font = `bold 11px ${HEI}`;
  x.textAlign = 'center';
  x.fillText('奖 状', 32, 18);
  x.fillStyle = '#333';
  x.font = `7px ${HEI}`;
  x.fillText(blank ? '______ 同学' : '小满 同学', 32, 29);
  x.fillText('三好学生', 32, 38);
  return toTex(c);
}

function calendar() {
  const [c, x] = canvas(32, 48);
  x.fillStyle = '#f0ece2';
  x.fillRect(0, 0, 32, 48);
  x.fillStyle = '#c0392b';
  x.fillRect(0, 0, 32, 12);
  x.fillStyle = '#fff3c4';
  x.font = `bold 8px ${HEI}`;
  x.textAlign = 'center';
  x.fillText('2000 · 4月', 16, 9);
  x.fillStyle = '#555';
  for (let d = 0; d < 30; d++) {
    const col = (d + 6) % 7;
    const row = Math.floor((d + 6) / 7);
    x.fillRect(3 + col * 4, 16 + row * 6, 2, 2);
  }
  // The 15th, circled in red crayon.
  x.strokeStyle = '#d0302a';
  x.beginPath();
  x.arc(3 + 6 * 4 + 1, 16 + 2 * 6 + 1, 3, 0, Math.PI * 2);
  x.stroke();
  return toTex(c);
}

function catDrawing() {
  const [c, x] = canvas(48, 40);
  x.fillStyle = '#efe9da';
  x.fillRect(0, 0, 48, 40);
  x.fillStyle = '#1d1b20';
  x.beginPath();
  x.ellipse(24, 26, 11, 7, 0, 0, Math.PI * 2);
  x.fill();
  x.beginPath();
  x.arc(33, 18, 5, 0, Math.PI * 2);
  x.fill();
  x.fillRect(30, 11, 2, 4);
  x.fillRect(35, 11, 2, 4);
  x.fillStyle = '#b8e04a';
  x.fillRect(34, 17, 1, 1);
  x.fillStyle = '#e05a3a';
  x.font = `9px ${KAI}`;
  x.fillText('煤球', 4, 10);
  return toTex(c);
}

function clockFace() {
  const [c, x] = canvas(32, 32);
  x.fillStyle = '#c8352b';
  x.fillRect(0, 0, 32, 32);
  x.fillStyle = '#f7f3e8';
  x.beginPath();
  x.arc(16, 16, 14, 0, Math.PI * 2);
  x.fill();
  x.fillStyle = '#222';
  x.font = `bold 7px ${HEI}`;
  x.textAlign = 'center';
  x.textBaseline = 'middle';
  x.fillText('12', 16, 6);
  x.fillText('3', 26, 16.5);
  x.fillText('6', 16, 26.5);
  x.fillText('9', 6, 16.5);
  return toTex(c);
}

function pencilLid() {
  const [c, x] = canvas(64, 24);
  x.fillStyle = '#3d7fc2';
  x.fillRect(0, 0, 64, 24);
  // A little rocket and the slogan printed on every tin pencil box.
  x.fillStyle = '#f1f1f1';
  x.beginPath();
  x.moveTo(6, 18);
  x.lineTo(12, 4);
  x.lineTo(18, 18);
  x.fill();
  x.fillStyle = '#e4572e';
  x.fillRect(10, 18, 4, 3);
  x.fillStyle = '#ffe36a';
  x.font = `bold 8px ${HEI}`;
  x.fillText('好好学习', 24, 11);
  x.fillText('天天向上', 24, 21);
  return toTex(c);
}

function rabbitFace() {
  const [c, x] = canvas(16, 16);
  x.fillStyle = '#f2b6c6';
  x.fillRect(0, 0, 16, 16);
  x.fillStyle = '#fff4f6';
  x.fillRect(4, 8, 8, 6);
  x.fillStyle = '#1a1a1a';
  x.fillRect(4, 5, 2, 2);
  x.fillRect(10, 5, 2, 2);
  x.fillStyle = '#d0506a';
  x.fillRect(7, 9, 2, 1);
  return toTex(c);
}

function notebookCover() {
  const [c, x] = canvas(32, 40);
  x.fillStyle = '#4b7cc4';
  x.fillRect(0, 0, 32, 40);
  x.fillStyle = '#f4f1e8';
  x.fillRect(4, 8, 24, 12);
  x.fillStyle = '#2a3f6a';
  x.font = `bold 8px ${HEI}`;
  x.textAlign = 'center';
  x.fillText('数 学', 16, 17);
  x.fillStyle = '#f4f1e8';
  x.font = `6px ${HEI}`;
  x.fillText('练习本', 16, 30);
  return toTex(c);
}

function note() {
  const [c, x] = canvas(32, 24);
  x.fillStyle = '#ece6d6';
  x.fillRect(0, 0, 32, 24);
  x.fillStyle = 'rgba(30,30,60,0.6)';
  for (let i = 5; i < 22; i += 4) x.fillRect(3, i, 18 + (i % 8), 1);
  return toTex(c);
}

function books() {
  const [c, x] = canvas(32, 16);
  const cols = ['#c0392b', '#2e86c1', '#f1c40f', '#27ae60', '#8e44ad', '#e67e22'];
  for (let i = 0; i < 8; i++) {
    x.fillStyle = cols[i % cols.length];
    x.fillRect(i * 4, 0, 4, 16);
  }
  return toTex(c);
}

// The seam: a bright thread with stitch marks, transparent around it.
function seam() {
  const [c, x] = canvas(16, 64);
  x.clearRect(0, 0, 16, 64);
  x.fillStyle = '#fff6e0';
  x.fillRect(7, 0, 2, 64);
  for (let i = 2; i < 64; i += 6) {
    x.fillRect(4, i, 8, 1);
  }
  return toTex(c);
}

function yarn() {
  const [c, x] = canvas(16, 16);
  const r = rng(311);
  x.fillStyle = '#c8402e';
  x.fillRect(0, 0, 16, 16);
  x.strokeStyle = 'rgba(255,190,170,0.6)';
  for (let i = 0; i < 6; i++) {
    x.beginPath();
    x.arc(r() * 16, r() * 16, 3 + r() * 6, r() * 3, r() * 3 + 2);
    x.stroke();
  }
  return toTex(c);
}

function bamboo() {
  const [c, x] = canvas(32, 8);
  x.fillStyle = '#cdb46a';
  x.fillRect(0, 0, 32, 8);
  x.fillStyle = '#8f7a3a';
  x.fillRect(10, 0, 1, 8);
  x.fillRect(24, 0, 1, 8);
  return toTex(c);
}

// The name tag on her cat wand, in her own crayon.
function wandTag() {
  const [c, x] = canvas(32, 16);
  x.fillStyle = '#f2ecdc';
  x.fillRect(0, 0, 32, 16);
  x.fillStyle = '#c0392b';
  x.font = `bold 11px ${KAI}`;
  x.textAlign = 'center';
  x.textBaseline = 'middle';
  x.fillText('满满', 16, 8.5);
  return toTex(c);
}

// A child's rubber ball in red, yellow and blue bands.
function ballSkin() {
  const [c, x] = canvas(32, 32);
  const r = rng(321);
  const bands = ['#d8402f', '#f2c94c', '#2f6fb8', '#f2c94c', '#d8402f'];
  bands.forEach((col, i) => {
    x.fillStyle = col;
    x.fillRect(0, i * 6.4, 32, 6.6);
  });
  x.fillStyle = '#fbf6ea';
  x.fillRect(13, 13, 6, 6);
  grain(x, 32, 32, r, 8);
  return toTex(c);
}

function rusty() {
  const [c, x] = canvas(16, 16);
  const r = rng(331);
  x.fillStyle = '#6e5446';
  x.fillRect(0, 0, 16, 16);
  blotches(x, 16, 16, r, 6, 'rgba(160,80,40,0.6)', 3);
  grain(x, 16, 16, r, 16);
  return toTex(c);
}

// Top of the nut: a hexagon with its hole.
function nutTop() {
  const [c, x] = canvas(16, 16);
  x.fillStyle = '#6e5446';
  x.fillRect(0, 0, 16, 16);
  x.fillStyle = '#8a6a55';
  x.beginPath();
  for (let i = 0; i < 6; i++) {
    const a = (i / 6) * Math.PI * 2;
    x.lineTo(8 + Math.cos(a) * 7, 8 + Math.sin(a) * 7);
  }
  x.fill();
  x.fillStyle = '#1a1210';
  x.beginPath();
  x.arc(8, 8, 3, 0, Math.PI * 2);
  x.fill();
  return toTex(c);
}

// The ring the nut becomes one page over: smooth, pale, faintly pearly.
function ringSkin() {
  const [c, x] = canvas(16, 16);
  const r = rng(341);
  x.fillStyle = '#e6ecef';
  x.fillRect(0, 0, 16, 16);
  blotches(x, 16, 16, r, 5, 'rgba(200,180,230,0.35)', 4);
  blotches(x, 16, 16, r, 4, 'rgba(180,230,220,0.3)', 4);
  return toTex(c);
}

// Dashes for the outline the prism shows where something hidden would be.
function ghostRing() {
  const [c, x] = canvas(32, 8);
  x.clearRect(0, 0, 32, 8);
  x.fillStyle = '#fff6e0';
  for (let i = 0; i < 32; i += 8) x.fillRect(i, 0, 5, 8);
  return toTex(c);
}

// Where the floor has fallen in: splintered board ends round a hole with no bottom. Transparent
// outside, so it lies on whatever floor is there.
function pit() {
  const [c, x] = canvas(32, 32);
  const r = rng(351);
  const edge = Array.from({ length: 18 }, () => 13 + r() * 3);
  const shape = (k) => {
    x.beginPath();
    edge.forEach((rr, i) => {
      const a = (i / edge.length) * Math.PI * 2;
      x.lineTo(16 + Math.cos(a) * rr * k, 16 + Math.sin(a) * rr * k);
    });
    x.closePath();
    x.fill();
  };
  x.fillStyle = '#3a2a22';
  shape(1);
  x.fillStyle = '#030203';
  shape(0.84);
  return toTex(c);
}

// A round shadow drawn the PS1 way: solid dots, thinning out toward the edge.
function blobShadow() {
  const [c, x] = canvas(16, 16);
  const bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
  x.fillStyle = '#000';
  for (let j = 0; j < 16; j++) {
    for (let i = 0; i < 16; i++) {
      const density = Math.max(0, 1 - Math.hypot(i - 7.5, j - 7.5) / 8) * 0.8;
      if (density > (bayer[(j % 4) * 4 + (i % 4)] + 0.5) / 16) x.fillRect(i, j, 1, 1);
    }
  }
  return toTex(c);
}

function hulaHoop() {
  const [c, x] = canvas(16, 8);
  x.fillStyle = '#ef7fa8';
  x.fillRect(0, 0, 16, 8);
  x.fillStyle = '#fbf3f6';
  x.fillRect(0, 0, 3, 8);
  x.fillRect(8, 0, 3, 8);
  return toTex(c);
}

function wallMark() {
  const [c, x] = canvas(16, 16);
  x.fillStyle = '#d7dbdf';
  x.fillRect(0, 0, 16, 16);
  return toTex(c);
}

function fur() {
  const [c, x] = canvas(16, 16);
  const r = rng(281);
  x.fillStyle = '#1c1b1f';
  x.fillRect(0, 0, 16, 16);
  grain(x, 16, 16, r, 14);
  return toTex(c);
}

function solid(color) {
  const [c, x] = canvas(8, 8);
  x.fillStyle = color;
  x.fillRect(0, 0, 8, 8);
  return toTex(c);
}

export function makePrologueTextures() {
  return {
    roomwall0: roomWall(0), roomwall1: roomWall(1), roomwall2: roomWall(2),
    plank0: plank(0), plank1: plank(1), plank2: plank(2),
    ceil0: ceil(0), ceil1: ceil(1), ceil2: ceil(2),
    door0: door('#79b0a2', true), door1: door('#8fa7a2', false), door2: door('#a9dccb', false),
    cabpaint0: paint('#79b0a2', 291), cabpaint1: paint('#8fa7a2', 292), cabpaint2: paint('#a9dccb', 293),
    cabinet0: cabinetFront('#79b0a2'), cabinet1: cabinetFront('#8fa7a2'), cabinet2: cabinetFront('#a9dccb'),
    window0: windowView(0), window1: windowView(1), window2: windowView(2),
    curtain: curtain(), quilt: quilt(), pillow: pillow(),
    bedwood: wood('#9a6a3e', 301), bedwood2: wood('#d9a27a', 302), deskwood: wood('#c9a26b', 303), stoolwood: wood('#a5713f', 304),
    wardrobe: wardrobeFront(), award: award(false), award2: award(true), calendar: calendar(), catdrawing: catDrawing(),
    lampgreen: solid('#3f8a5a'), lampbulb: solid('#fff4cf'),
    clockred: solid('#c8352b'), clockface: clockFace(), clockhand: solid('#202020'), clocksec: solid('#d0302a'),
    tinblue: solid('#3d7fc2'), pencillid: pencilLid(),
    pinkcloth: solid('#f2b6c6'), rabbitface: rabbitFace(),
    nbcover: notebookCover(), note: note(), books: books(), wallmark: wallMark(),
    seam: seam(), fur: fur(), cateye: solid('#b8e04a'), catnose: solid('#d07a8a'),
    yarn: yarn(), bamboo: bamboo(), wandtag: wandTag(),
    ballskin: ballSkin(), rusty: rusty(), nuttop: nutTop(), ringskin: ringSkin(), ghostring: ghostRing(),
    pit: pit(), hulahoop: hulaHoop(), blobshadow: blobShadow(),
  };
}

// How brightly each texture lights itself; the rest default to 0.
export const PROLOGUE_EMISSIVE = {
  lampbulb: 1, cateye: 1.2, seam: 1.4, window0: 0.35, window1: 0.15, window2: 0.45,
};
export const PROLOGUE_ALPHA = new Set(['seam', 'ghostring', 'pit', 'blobshadow']);
