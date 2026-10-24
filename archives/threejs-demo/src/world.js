import { CELL, H } from './level.js';

// Three pages of the same apartment, stacked along W. Grid cells are 0.5 m.
// '#' wall  'M' flesh  'L' locked door  'F' front door  'B' junk pile (blocks)
// '.' terrazzo  ',' wood floor  ';' tiles  'D' doorway

const repeat = (row, n) => Array(n).fill(row);

const MAP0 = [
  '############################',
  ...repeat('#,,,,,,,,,#,,,,,,,,,,#;;;;;#', 8),
  '######LL#######DD######DD###',
  '#BBBBBBBBBB................F',
  '#BBBBBBBBBB................F',
  '#BBBBBBBBBB................#',
  '###########DDD#####DD#######',
  ...repeat('#..............#;;;;;;;;;;;#', 7),
  '############################',
];

const MAP1 = [
  '############################',
  ...repeat('###########,,,,,,,,,,#######', 8),
  '###############DD###########',
  '#..........................F',
  '#..........................F',
  '#..........................#',
  '###########DDD#####DD#######',
  ...repeat('#..............#;;;;;;;;;;;#', 2),
  ...repeat('#...............;;;;;;;;;;;#', 2),
  ...repeat('#..............#;;;;;;;;;;;#', 3),
  '############################',
];

const MAP2 = [
  '###########MMMMMMMMMMMMMMMMM',
  ...repeat('#,,,,,,,,,#MMMMMMMMMMMMMMMMM', 8),
  '######DD###MMMMMMMMMMMMMMMMM',
  'M.........MMMMMMMMMMMMMMMMMM',
  'M..............MMMMMMMMMMMMM',
  'M..............MMMMMMMMMMMMM',
  ...repeat('MMMMMMMMMMMMMMMMMMMMMMMMMMMM', 9),
];

function rand(seed) {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

function table(L, tex, x0, z0, x1, z1, h) {
  L.box(tex, x0, h - 0.04, z0, x1, h, z1, { solid: true });
  const t = 0.04;
  for (const [x, z] of [[x0, z0], [x1 - t, z0], [x0, z1 - t], [x1 - t, z1 - t]]) {
    L.box(tex, x, 0, z, x + t, h - 0.04, z + t);
  }
}

function bed(L, frame, cloth, x0, z0, x1, z1, headZ) {
  L.box(frame, x0, 0, z0, x1, 0.35, z1, { solid: true });
  L.box(cloth, x0 + 0.02, 0.35, z0 + 0.02, x1 - 0.02, 0.5, z1 - 0.02);
  if (headZ !== undefined) L.box(frame, x0, 0, headZ, x1, 0.85, headZ + 0.06);
}

// Wardrobe with a slit between its doors. The interior is an inward-facing shell, so the
// camera sees dark wood when hiding inside and backface culling hides it from outside.
function wardrobe(L, x0, z0, x1, z1, h) {
  const zc = (z0 + z1) / 2;
  const g = 0.025;
  const t = 0.03;
  const dark = [0.03, 0.025, 0.02];
  L.box('darkwood', x0, 0, z0, x1, h, z1, { skip: 'px' });
  L.face('darkwood', [x1, 0, z1], [0, 0, -1], [0, 1, 0], z1 - zc - g, h);
  L.face('darkwood', [x1, 0, zc - g], [0, 0, -1], [0, 1, 0], zc - g - z0, h);
  L.box('darkwood', x0 + t, t, z0 + t, x1 - t, h - t, z1 - t, { skip: 'px', flip: true, color: dark });
  L.face('darkwood', [x1 - t, t, z1 - t], [0, 0, -1], [0, 1, 0], z1 - t - zc - g, h - 2 * t, { flip: true, color: dark });
  L.face('darkwood', [x1 - t, t, zc - g], [0, 0, -1], [0, 1, 0], zc - g - z0 - t, h - 2 * t, { flip: true, color: dark });
  L.colliders.push({ x0, z0, x1, z1 });
}

export const WARDROBE = { x: 5.7, z: 3.3, exitX: 6.55 };

const LAYER0 = {
  map: MAP0,
  tex: { wall: 'wall0', floor: 'terrazzo0', wood: 'woodfloor0', tile: 'tile0', ceiling: 'ceiling0' },
  fog: [0.025, 0.018, 0.012], fogNear: 2.5, fogFar: 12,
  ambient: [0.06, 0.05, 0.04],
  lights: [
    { x: 9.5, y: 2.55, z: 5.75, color: [1.0, 0.8, 0.55], intensity: 1.15, range: 6.5, fixture: 'bulb' },
    { x: 3.75, y: 2.55, z: 8.75, color: [1.0, 0.8, 0.55], intensity: 1.05, range: 7, fixture: 'bulb' },
    { x: 11.0, y: 2.72, z: 8.75, color: [0.8, 0.95, 0.9], intensity: 0.75, range: 6, fixture: 'tube' },
    { x: 9.25, y: 0.75, z: 0.8, color: [1.0, 0.7, 0.45], intensity: 0.7, range: 4.5 },
    { x: 8.3, y: 2.55, z: 3.2, color: [1.0, 0.78, 0.5], intensity: 0.7, range: 5.5, fixture: 'bulb' },
    { x: 3.95, y: 1.0, z: 0.75, color: [1.0, 0.78, 0.5], intensity: 1.0, range: 5 },
  ],
  props(L) {
    // Child's room (x 0.5–5.0, z 0.5–4.5): sealed in this page.
    bed(L, 'wood', 'quilt', 0.5, 1.8, 1.45, 3.8, 1.74);
    L.box('sheet', 0.6, 0.5, 1.85, 1.35, 0.6, 2.2);
    table(L, 'wood', 2.9, 0.5, 4.2, 1.05, 0.76);
    L.cyl('steel', 3.95, 0.76, 0.75, 0.07, 0.05, 0.03, 6);
    L.box('steel', 3.94, 0.79, 0.74, 3.96, 1.0, 0.76);
    L.cyl('steel', 3.95, 0.98, 0.75, 0.13, 0.05, 0.12, 6, { open: true });
    L.box('bulb', 3.92, 0.97, 0.72, 3.98, 1.0, 0.78, { color: [0, 0, 0] });
    L.box('drawing0', 3.15, 0.76, 0.62, 3.6, 0.765, 0.98);
    L.decal('paper', 3.75, 0.765, 0.85, '+y', 0.16, 0.2);
    L.item({ id: 'final', x: 3.45, y: 0.8, z: 0.85, prompt: '看桌上的画', note: 'final' });
    L.box('wood', 3.3, 0, 1.25, 3.7, 0.45, 1.6, { solid: true });
    L.decal('window', 1.9, 1.6, 0.5, '+z', 1.0, 1.0);
    L.decal('drawing1', 5.0, 1.4, 2.0, '-x', 0.48, 0.4);
    L.decal('drawing2', 5.0, 1.3, 2.8, '-x', 0.48, 0.4);
    L.decal('door', 3.5, 1.05, 4.5, '-z', 0.95, 2.1);
    L.cyl('quilt', 4.4, 0, 3.6, 0.12, 0.12, 0.2, 6);
    L.item({ id: 'latch', x: 3.5, y: 1.0, z: 4.4, prompt: '看门', text: '插销是从里面插上的。' });

    // Parents' room (x 5.5–10.5, z 0.5–4.5).
    bed(L, 'wood', 'quilt', 7.0, 0.56, 8.8, 2.6, 0.5);
    L.box('sheet', 7.1, 0.5, 0.62, 8.7, 0.6, 0.95);
    L.box('wood', 9.0, 0, 0.55, 9.5, 0.5, 1.05, { solid: true });
    L.cyl('wood', 9.25, 0.5, 0.75, 0.05, 0.03, 0.18, 6);
    L.cyl('sheet', 9.25, 0.66, 0.75, 0.12, 0.07, 0.16, 6, { open: true });
    L.decal('paper', 9.22, 0.5, 0.95, '+y', 0.16, 0.2);
    L.item({ id: 'note3', x: 9.22, y: 0.55, z: 0.95, prompt: '看', note: 'n3' });
    wardrobe(L, 5.5, 2.7, 6.15, 3.9, 2.0);
    L.item({ id: 'wardrobe', x: 6.2, y: 1.2, z: 3.3, prompt: '躲进衣柜', kind: 'hide' });
    L.box('wood', 10.0, 0, 2.8, 10.5, 0.8, 3.8, { solid: true });

    // Bathroom (x 11.0–13.5, z 0.5–4.5).
    L.box('enamel', 13.0, 0.6, 1.5, 13.5, 0.8, 2.1, { solid: true });
    L.box('enamel', 13.15, 0, 1.7, 13.35, 0.6, 1.9);
    L.decal('mirror', 13.5, 1.45, 1.8, '-x', 0.45, 0.55);
    L.item({ id: 'mirror', x: 13.3, y: 1.45, z: 1.8, prompt: '看镜子', kind: 'mirror' });
    L.box('enamel', 13.1, 1.9, 3.3, 13.5, 2.5, 3.9);
    L.cyl('steel', 11.5, 0, 1.0, 0.16, 0.19, 0.32, 7, { solid: true });

    // Corridor (x 5.5–13.5 walkable, z 5.0–6.5).
    L.box('wood', 12.3, 0, 5.0, 13.3, 0.85, 5.35, { solid: true });
    L.box('stereoscope', 12.7, 0.85, 5.1, 12.92, 0.93, 5.22, { item: 'scope' });
    L.item({ id: 'scope', x: 12.8, y: 0.9, z: 5.16, prompt: '拿起立体镜', kind: 'scope' });
    L.decal('calendar', 10.0, 1.55, 5.0, '+z', 0.3, 0.45);
    L.decal('frontdoor', 13.5, 1.05, 5.5, '-x', 0.95, 2.1);
    L.item({ id: 'frontdoor', x: 13.4, y: 1.1, z: 5.5, prompt: '开门', text: '门打不开。\n猫眼外面是一片白。' });
    L.decal('door', 3.5, 1.05, 5.0, '+z', 0.95, 2.1);
    const r = rand(5);
    for (let cz = 10; cz <= 12; cz++) {
      for (let cx = 1; cx <= 10; cx++) {
        const x0 = cx * CELL;
        const z0 = cz * CELL;
        let y = 0;
        const n = 2 + Math.floor(r() * 3);
        for (let k = 0; k < n && y < 2.2; k++) {
          const h = 0.3 + r() * 0.35;
          const inset = 0.02 + r() * 0.05;
          const front = cx === 10 ? CELL : CELL - inset;
          L.box(r() < 0.75 ? 'cardboard' : 'darkwood', x0 + inset, y, z0 + inset, x0 + front, y + h, z0 + CELL - inset);
          y += h;
        }
      }
    }
    L.decal('paper', 5.5, 1.1, 5.75, '+x', 0.2, 0.26);
    L.item({ id: 'junknote', x: 5.6, y: 1.1, z: 5.75, prompt: '看纸条', note: 'junk' });

    // Living room (x 0.5–7.5, z 7.0–10.5).
    L.box('sofa', 1.8, 0, 9.75, 4.2, 0.42, 10.5, { solid: true });
    L.box('sofa', 1.8, 0.42, 10.25, 4.2, 0.85, 10.5);
    L.box('sofa', 1.8, 0.42, 9.75, 2.0, 0.62, 10.25);
    L.box('sofa', 4.0, 0.42, 9.75, 4.2, 0.62, 10.25);
    table(L, 'wood', 2.4, 8.6, 3.6, 9.2, 0.4);
    L.decal('paper', 3.0, 0.4, 8.9, '+y', 0.2, 0.26);
    L.item({ id: 'note1', x: 3.0, y: 0.45, z: 8.9, prompt: '看', note: 'n1' });
    L.cyl('enamel', 2.65, 0.4, 8.8, 0.04, 0.04, 0.09, 6);
    L.box('darkwood', 2.3, 0, 7.0, 3.7, 0.55, 7.45, { solid: true });
    L.box('black', 2.7, 0.55, 7.05, 3.3, 1.05, 7.5);
    L.decal('crt', 3.0, 0.8, 7.5, '+z', 0.56, 0.46);
    table(L, 'wood', 5.6, 8.6, 6.6, 9.4, 0.76);
    L.box('wood', 5.85, 0, 9.5, 6.25, 0.45, 9.9, { solid: true });
    L.decal('fu', 0.5, 1.6, 8.75, '+x', 0.45, 0.45);
    L.decal('clock', 5.0, 2.1, 10.5, '-z', 0.32, 0.32);

    // Kitchen (x 8.0–13.5, z 7.0–10.5).
    L.box('enamel', 8.02, 0, 7.02, 8.65, 1.6, 7.65, { solid: true });
    L.box('tile0', 12.9, 0, 7.6, 13.5, 0.85, 10.45, { solid: true, uv: 0.5 });
    L.box('steel', 12.95, 0.85, 8.0, 13.45, 0.95, 8.6);
    L.cyl('steel', 13.2, 0.95, 8.3, 0.14, 0.14, 0.16, 7);
    L.box('wood', 13.15, 1.6, 7.6, 13.5, 2.3, 9.4);
    table(L, 'wood', 10.2, 9.2, 11.1, 9.9, 0.76);
    L.cyl('bowl', 10.5, 0.76, 9.5, 0.05, 0.08, 0.06, 7, { item: 'food1' });
    L.cyl('rice', 10.5, 0.82, 9.5, 0.075, 0.03, 0.03, 7, { item: 'food1' });
    L.item({ id: 'food1', x: 10.5, y: 0.8, z: 9.5, prompt: '吃', kind: 'food' });
    L.decal('paper', 10.85, 0.76, 9.6, '+y', 0.18, 0.24);
    L.item({ id: 'note2', x: 10.85, y: 0.8, z: 9.6, prompt: '看', note: 'n2' });
    L.cyl('bun', 13.2, 0.85, 9.9, 0.07, 0.05, 0.06, 6, { item: 'food2' });
    L.item({ id: 'food2', x: 13.2, y: 0.9, z: 9.9, prompt: '吃', kind: 'food' });
  },
};

const LAYER1 = {
  map: MAP1,
  tex: { wall: 'wall1', floor: 'terrazzo1', wood: 'woodfloor1', tile: 'tile1', ceiling: 'ceiling1' },
  fog: [0.06, 0.068, 0.078], fogNear: 1.0, fogFar: 9,
  ambient: [0.05, 0.06, 0.075],
  lights: [
    { x: 7.0, y: 2.55, z: 5.75, color: [0.6, 0.7, 0.85], intensity: 0.9, range: 7, fixture: 'bulb' },
    { x: 3.75, y: 2.55, z: 8.75, color: [0.6, 0.7, 0.85], intensity: 0.7, range: 6, fixture: 'bulb' },
    { x: 8.0, y: 2.55, z: 2.5, color: [0.55, 0.65, 0.8], intensity: 0.5, range: 5, fixture: 'bulb' },
    { x: 11.0, y: 2.55, z: 8.75, color: [0.5, 0.6, 0.75], intensity: 0.35, range: 5, fixture: 'bulb' },
  ],
  props(L) {
    // Everything is under white sheets in this page.
    L.box('sheet', 1.75, 0, 9.7, 4.25, 0.9, 10.5, { solid: true, uv: 1 });
    L.box('sheet', 2.35, 0, 8.55, 3.65, 0.45, 9.25, { solid: true, uv: 1 });
    L.box('sheet', 2.25, 0, 7.0, 3.75, 1.1, 7.5, { solid: true, uv: 1 });
    L.box('sheet', 5.55, 0, 8.55, 6.65, 0.8, 9.45, { solid: true, uv: 1 });
    L.decal('paper', 3.0, 0.45, 8.9, '+y', 0.3, 0.22);
    L.item({ id: 'note5', x: 3.0, y: 0.5, z: 8.9, prompt: '看报纸', note: 'n5' });

    L.box('sheet', 8.9, 0, 5.75, 9.3, 1.5, 6.15, { solid: true, uv: 1 });
    L.box('sheet', 8.98, 1.5, 5.83, 9.22, 1.75, 6.07, { uv: 1 });
    L.box('sheet', 2.0, 0, 5.05, 2.5, 0.9, 5.55, { solid: true, uv: 1 });
    L.box('sheet', 4.2, 0, 6.0, 4.7, 0.9, 6.45, { solid: true, uv: 1 });
    L.decal('frontdoor', 13.5, 1.05, 5.5, '-x', 0.95, 2.1);

    L.box('sheet', 6.95, 0, 0.5, 8.85, 0.6, 2.65, { solid: true, uv: 1 });
    L.box('sheet', 5.5, 0, 2.65, 6.2, 2.05, 3.95, { solid: true, uv: 1 });

    L.box('sheet', 8.0, 0, 7.0, 8.7, 1.65, 7.7, { solid: true, uv: 1 });
    table(L, 'wood', 10.2, 9.2, 11.1, 9.9, 0.76);
    L.cyl('bowl', 10.6, 0.76, 9.55, 0.05, 0.08, 0.06, 7, { item: 'food3' });
    L.cyl('rice', 10.6, 0.82, 9.55, 0.075, 0.03, 0.03, 7, { item: 'food3' });
    L.item({ id: 'food3', x: 10.6, y: 0.8, z: 9.55, prompt: '吃', kind: 'food' });
  },
};

const LAYER2 = {
  map: MAP2,
  tex: { wall: 'wall2', floor: 'terrazzo2', wood: 'woodfloor2', tile: 'tile2', ceiling: 'ceiling2' },
  fog: [0.07, 0.008, 0.004], fogNear: 1.0, fogFar: 8,
  ambient: [0.12, 0.04, 0.03],
  lights: [
    { x: 2.5, y: 2.4, z: 5.75, color: [1.0, 0.3, 0.18], intensity: 2.0, range: 6.5 },
    { x: 6.6, y: 1.0, z: 6.0, color: [1.0, 0.4, 0.2], intensity: 1.5, range: 2.8 },
    { x: 2.6, y: 2.3, z: 2.5, color: [1.0, 0.28, 0.16], intensity: 1.8, range: 5.5 },
  ],
  props(L) {
    bed(L, 'rotwood', 'rotcloth', 0.5, 1.8, 1.45, 3.8, 1.74);
    table(L, 'rotwood', 2.9, 0.5, 4.2, 1.05, 0.76);
    L.box('door2', 3.0, 0, 3.55, 3.05, 2.05, 4.48, { solid: true });
    const r = rand(9);
    for (let i = 0; i < 16; i++) {
      const x = 0.8 + r() * 4.0;
      const z = 0.8 + r() * 3.4;
      const len = 0.5 + r() * 1.5;
      L.box('flesh', x - 0.012, H - len, z - 0.012, x + 0.012, H, z + 0.012);
    }
    L.decal('writing', 7.5, 1.25, 6.0, '-x', 0.95, 0.36);
    L.item({ id: 'writing', x: 7.35, y: 1.25, z: 6.0, prompt: '看墙上的字', note: 'wall' });
    L.decal('paper', 2.2, 0.0, 5.9, '+y', 0.2, 0.26);
    L.item({ id: 'note4', x: 2.2, y: 0.1, z: 5.9, prompt: '看', note: 'n4' });
  },
};

export const LAYER_DEFS = [LAYER0, LAYER1, LAYER2];
export const SPAWN = { x: 12.6, z: 5.85, yaw: Math.PI / 2 };
