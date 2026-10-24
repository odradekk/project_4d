import { EPS } from './four.js';

// Three pages of the same room. They share one 10 m x 8 m grid of 0.5 m cells, so a point (x, z)
// means the same place in every page; only the W position differs.
// Ⅰ is her room; Ⅱ is the next page over, the same built-in shell with none of her things in it;
// Ⅲ lies much deeper and has drifted: bigger, brighter, slightly wrong.

export const W_POS = [0, 0.42, 1.6];
export const ROMAN = ['Ⅰ', 'Ⅱ', 'Ⅲ'];

const grid = (open) =>
  Array.from({ length: 16 }, (_, r) => Array.from({ length: 20 }, (_, c) => (open(r, c) ? ',' : '#')).join(''));
const SMALL = grid((r, c) => r >= 5 && r <= 11 && c >= 6 && c <= 13); // x 3.0–7.0, z 2.5–6.0
const BIG = grid((r, c) => r >= 1 && r <= 14 && c >= 1 && c <= 18); // x 0.5–9.5, z 0.5–7.5

const texSet = (v) => ({ wall: `roomwall${v}`, floor: `plank${v}`, wood: `plank${v}`, tile: `plank${v}`, ceiling: `ceil${v}` });

// The flat's own fittings are part of the building, so pages close to each other all have them.
// In her room the cabinet's front is the hinged flap built in main.js, so no painted doors there.
function shell(L, v) {
  L.box(`cabpaint${v}`, 3.25, 2.0, 5.55, 4.45, 2.4, 6.0, { skip: 'nz', building: true });
  if (v !== 0) L.decal(`cabinet${v}`, 3.85, 2.2, 5.55, '-z', 1.2, 0.4);
  L.decal(`door${v}`, 3.85, 1.0, 6.0, '-z', 0.9, 2.0);
  L.decal(`window${v}`, 5.6, 1.6, 2.5, '+z', 1.2, 1.1);
  L.box(`ceil${v}`, 4.95, 0.98, 2.5, 6.25, 1.03, 2.64, { building: true });
}

const ROOM = {
  map: SMALL,
  height: 3.0,
  tex: texSet(0),
  fog: [0.03, 0.026, 0.032], fogNear: 3, fogFar: 14,
  ambient: [0.1, 0.09, 0.1],
  glow: [0.3, 0.27, 0.22],
  shade: 0.6,
  lights: [
    { x: 6.72, y: 1.0, z: 2.92, color: [1.0, 0.8, 0.52], intensity: 1.6, range: 3.8 },
    { x: 5.0, y: 2.8, z: 4.25, color: [1.0, 0.86, 0.66], intensity: 1.2, range: 6.5, fixture: 'bulb' },
  ],
  surfaces: [
    { x0: 6.4, z0: 2.7, x1: 7.0, z1: 3.9, y: 0.76 },
    { x0: 3.0, z0: 2.6, x1: 3.95, z1: 4.6, y: 0.5 },
    { x0: 4.95, z0: 2.5, x1: 6.25, z1: 2.76, y: 1.03 },
  ],
  // Flipping something on the far side of one of these planes brings it round to her side.
  flipPlanes: [{ axis: 'z', at: 2.76, x0: 4.95, x1: 6.25, name: '栏杆' }],
  props(L) {
    shell(L, 0);
    L.box('curtain', 4.72, 0.95, 2.52, 4.98, 2.35, 2.6);
    L.box('curtain', 6.22, 0.95, 2.52, 6.48, 2.35, 2.6);
    L.box('black', 4.68, 2.36, 2.53, 6.52, 2.38, 2.55);
    // A deep sill behind a guard rail, the kind fitted so children can't climb out of the window.
    L.box('ceil0', 4.95, 0.98, 2.64, 6.25, 1.03, 2.76);
    for (let x = 5.0; x < 6.25; x += 0.1) L.box('steel', x - 0.008, 1.03, 2.75, x + 0.008, 2.3, 2.766);
    L.box('steel', 4.95, 1.03, 2.745, 6.25, 1.06, 2.77);
    L.box('steel', 4.95, 2.27, 2.745, 6.25, 2.3, 2.77);
    L.colliders.push({ x0: 4.95, z0: 2.5, x1: 6.25, z1: 2.8 });

    // Bed along the west wall.
    L.box('bedwood', 3.0, 0, 2.6, 3.95, 0.32, 4.6, { solid: true });
    L.box('quilt', 3.02, 0.32, 3.05, 3.93, 0.5, 4.58);
    L.box('pillow', 3.1, 0.32, 2.66, 3.85, 0.46, 3.0);
    L.box('bedwood', 3.0, 0, 2.5, 3.95, 0.8, 2.6);

    // Desk under the lamp on the east wall.
    L.box('deskwood', 6.4, 0.72, 2.7, 7.0, 0.76, 3.9, { solid: true });
    for (const [x, z] of [[6.42, 2.72], [6.94, 2.72], [6.42, 3.84], [6.94, 3.84]]) L.box('deskwood', x, 0, z, x + 0.04, 0.72, z + 0.04);
    L.box('deskwood', 6.45, 0.56, 3.3, 6.98, 0.72, 3.84);
    L.cyl('lampgreen', 6.8, 0.76, 2.88, 0.07, 0.06, 0.02, 8);
    L.box('lampgreen', 6.79, 0.78, 2.87, 6.81, 1.02, 2.89);
    L.cyl('lampgreen', 6.74, 0.96, 2.92, 0.13, 0.05, 0.11, 8, { open: true });
    L.box('lampbulb', 6.71, 0.95, 2.89, 6.77, 0.98, 2.95, { color: [0, 0, 0] });
    L.box('books', 6.75, 0.76, 3.6, 6.98, 0.88, 3.86);
    L.decal('nbcover', 6.6, 0.76, 3.44, '+y', 0.19, 0.24);
    L.decal('note', 6.52, 0.76, 3.05, '+y', 0.14, 0.1);

    // Wardrobe; the gap between it and the desk is where the cat keeps staring.
    L.box('deskwood', 6.45, 0, 4.6, 7.0, 1.9, 5.9, { solid: true, skip: 'nx' });
    L.decal('wardrobe', 6.45, 0.95, 5.25, '-x', 1.3, 1.9);

    L.decal('award', 3.0, 1.75, 3.6, '+x', 0.6, 0.45);
    L.decal('calendar', 4.25, 1.75, 2.5, '+z', 0.32, 0.46);
    L.decal('catdrawing', 3.0, 1.4, 4.6, '+x', 0.42, 0.35);

    L.item({ id: 'notebook', x: 6.6, y: 0.8, z: 3.44, prompt: '看笔记本', kind: 'notebook' });
    L.item({ id: 'note', x: 6.52, y: 0.8, z: 3.05, prompt: '看字条', text: '爸爸的字条：\n"饭在锅里，热一下再吃。作业写完早点睡。——爸"' });
    L.item({ id: 'door', x: 3.85, y: 1.0, z: 5.9, prompt: '开门', text: '客厅里黑漆漆的。\n爸爸还没下班……我才不出去。' });
    L.item({ id: 'window', x: 5.6, y: 1.9, z: 2.62, prompt: '看窗外', text: '对面楼的灯，一盏一盏都亮着。' });
    L.item({ id: 'flap', x: 3.85, y: 2.2, z: 5.5, prompt: '打开吊柜的翻板门', kind: 'flap' });
    L.item({ id: 'award', x: 3.08, y: 1.75, z: 3.6, prompt: '看', text: '三好学生。\n爸爸把它贴在了最显眼的地方。' });
    L.item({ id: 'calendar', x: 4.25, y: 1.75, z: 2.58, prompt: '看', text: '十五号画了个圈。\n星期六，爸爸说要带我去公园。' });
    L.item({ id: 'drawing', x: 3.08, y: 1.4, z: 4.6, prompt: '看', text: '我画的煤球。\n爸爸说画得像个煤球。它本来就叫煤球嘛。' });
    L.item({ id: 'hula', x: 3.14, y: 0.45, z: HULA.z, prompt: '看', text: '我的呼啦圈。\n煤球最会钻圈了，一逗它就跳过去。\n平时它只扑，不跳。' });
  },
};

const EMPTY = {
  map: SMALL,
  height: 3.0,
  tex: texSet(1),
  fog: [0.1, 0.11, 0.13], fogNear: 2, fogFar: 11,
  ambient: [0.15, 0.16, 0.2],
  glow: [0.22, 0.24, 0.28],
  shade: 0.5,
  lights: [{ x: 5.6, y: 1.6, z: 2.85, color: [0.6, 0.68, 0.86], intensity: 1.3, range: 7 }],
  surfaces: [{ x0: 4.95, z0: 2.5, x1: 6.25, z1: 2.64, y: 1.03 }],
  props(L) {
    shell(L, 1);
    L.decal('wallmark', 3.0, 1.75, 3.6, '+x', 0.62, 0.47);
    L.item({ id: 'window', x: 5.6, y: 1.5, z: 2.62, prompt: '看窗外', text: '对面楼一盏灯也没有。' });
    L.item({ id: 'door', x: 3.85, y: 1.0, z: 5.9, prompt: '开门', text: '门推不开。' });
    L.item({ id: 'mark', x: 3.08, y: 1.75, z: 3.6, prompt: '看', text: '墙上有一块颜色浅一点，\n像是贴过什么东西。' });
    // In this page the floor in front of where the board comes down has fallen in.
    const px = (PIT.x0 + PIT.x1) / 2;
    const pz = (PIT.z0 + PIT.z1) / 2;
    // Kept just under the slices of things stood in W, which lie flat on the floor.
    L.decal('pit', px, 0, pz, '+y', PIT.x1 - PIT.x0 + 0.12, PIT.z1 - PIT.z0 + 0.12);
    L.colliders.push({ ...PIT });
    L.item({ id: 'pit', x: px, y: 0.1, z: pz, prompt: '看', text: '地板塌了一大块，\n底下黑漆漆的，看不到底。' });
  },
};

const DEEP = {
  map: BIG,
  height: 4.2,
  tex: texSet(2),
  fog: [0.86, 0.74, 0.75], fogNear: 5, fogFar: 18,
  ambient: [0.26, 0.21, 0.23],
  glow: [0.25, 0.22, 0.22],
  shade: 0.9,
  lights: [
    { x: 5.0, y: 2.4, z: 1.2, color: [1.0, 0.8, 0.72], intensity: 1.8, range: 9 },
    { x: 5.0, y: 3.9, z: 5.0, color: [0.95, 0.82, 0.88], intensity: 0.9, range: 9 },
    { x: 8.5, y: 2.45, z: 1.45, color: [1.0, 0.85, 0.6], intensity: 0.8, range: 5 },
  ],
  surfaces: [{ x0: 0.6, z0: 1.5, x1: 1.6, z1: 6.5, y: 0.32 }],
  props(L) {
    L.decal('window2', 5.0, 2.3, 0.5, '+z', 5.0, 2.8);
    L.box('ceil2', 2.4, 0.86, 0.5, 7.6, 0.9, 0.66);
    L.decal('door2', 3.5, 1.0, 7.5, '-z', 0.9, 2.0);
    L.decal('door2', 6.8, 1.0, 7.5, '-z', 0.9, 2.0);
    L.box('cabpaint2', 2.8, 3.3, 7.05, 4.2, 3.8, 7.5, { skip: 'nz' });
    L.decal('cabinet2', 3.5, 3.55, 7.05, '-z', 1.4, 0.5);
    L.box('bedwood2', 0.6, 0, 1.5, 1.6, 0.32, 6.5, { solid: true });
    L.cyl('lampgreen', 8.6, 0, 1.4, 0.18, 0.15, 0.04, 8);
    L.box('lampgreen', 8.58, 0.04, 1.38, 8.62, 2.6, 1.42);
    L.cyl('lampgreen', 8.5, 2.5, 1.45, 0.45, 0.18, 0.35, 8, { open: true });
    L.box('lampbulb', 8.42, 2.45, 1.37, 8.58, 2.55, 1.53, { color: [0, 0, 0] });
    L.decal('award2', 0.5, 2.6, 4.0, '+x', 1.5, 1.1);
    L.item({ id: 'window', x: 5.0, y: 1.6, z: 0.65, prompt: '看窗外', text: '外面好亮……\n现在明明是晚上。' });
    L.item({ id: 'doorA', x: 3.5, y: 1.0, z: 7.4, prompt: '开门', text: '门打不开。' });
    L.item({ id: 'doorB', x: 6.8, y: 1.0, z: 7.4, prompt: '开门', text: '这扇门后面……是墙。' });
    L.item({ id: 'award2', x: 0.6, y: 2.0, z: 4.0, prompt: '看', text: '奖状上没有名字。' });
  },
};

export const SECTION_DEFS = [ROOM, EMPTY, DEEP];

// Seams only connect the pages listed; s2 also turns whoever crosses it into a mirror image.
// s2 lies just short of s1 on the cat's run-up to a hoop set at s1, and can't be seen from page Ⅰ.
export const SEAMS = [
  { id: 's1', a: 0, b: 1, x: 6.75, z: 4.25 },
  { id: 's2', a: 1, b: 2, x: 6.05, z: 4.15, mirror: true },
];

// A hole in page Ⅱ's floor, just in front of where the board's foot comes down: wider than the
// board, too wide for the cat to jump. In W it runs from 0.3 to 0.6, around page Ⅱ only.
export const PIT = { x0: 4.4, z0: 3.95, x1: 5.5, z1: 4.5, w0: 0.3, w1: 0.6 };

// The buildings in 4D. Pages close together share one building, so its walls, floor and ceiling
// run on through W across them: from before page Ⅰ to past page Ⅱ, with the hole cut from the
// floor around page Ⅱ only. Page Ⅲ has drifted and is a building of its own.
const BUILDINGS = [
  { x0: 3.0, z0: 2.5, x1: 7.0, z1: 6.0, h: 3.0, w0: -0.2, w1: 0.9, hole: PIT },
  { x0: 0.5, z0: 0.5, x1: 9.5, z1: 7.5, h: 4.2, w0: 1.3, w1: 1.9 },
];
const buildingOf = (w) => BUILDINGS.find((b) => w >= b.w0 && w <= b.w1);

// The floor as (x, z, w) rectangles, less the hole where there is one.
function floorPieces({ x0, z0, x1, z1, w0, w1, hole: p }) {
  if (!p) return [[x0, z0, w0, x1, z1, w1]];
  return [
    [x0, z0, w0, x1, z1, p.w0],
    [x0, z0, p.w0, p.x0, z1, p.w1],
    [p.x1, z0, p.w0, x1, z1, p.w1],
    [p.x0, z0, p.w0, p.x1, p.z0, p.w1],
    [p.x0, p.z1, p.w0, p.x1, z1, p.w1],
    [x0, z0, p.w1, x1, z1, w1],
  ];
}

// What things rest on, as 4D boxes: a rectangle in x-z, a range in W, and a top at height y.
// Besides the floors, furniture and sills are as thin in W as anything else people make.
const support = (x0, z0, x1, z1, w0, w1, y) => ({ x0, z0, x1, z1, w0, w1, y });
export const SUPPORTS = [
  ...BUILDINGS.flatMap((b) => floorPieces(b).map(([x0, z0, w0, x1, z1, w1]) => support(x0, z0, x1, z1, w0, w1, 0))),
  ...SECTION_DEFS.flatMap((d, i) => d.surfaces.map((s) => support(s.x0, s.z0, s.x1, s.z1, W_POS[i] - EPS, W_POS[i] + EPS, s.y))),
];

// Everything solid, as 4D boxes { x0, y0, z0, w0, x1, y1, z1, w1 }: the buildings' walls, floors and
// ceilings, and every box drawn in a page. A box marked as part of the building runs through W
// with it; anything else drawn is furniture, EPS thick in W about its page.
export function worldSolids(layers) {
  const T = 0.3;
  const out = [];
  const add = (x0, y0, z0, w0, x1, y1, z1, w1) => out.push({ x0, y0, z0, w0, x1, y1, z1, w1 });
  for (const b of BUILDINGS) {
    add(b.x0 - T, -T, b.z0 - T, b.w0, b.x0, b.h + T, b.z1 + T, b.w1);
    add(b.x1, -T, b.z0 - T, b.w0, b.x1 + T, b.h + T, b.z1 + T, b.w1);
    add(b.x0, -T, b.z0 - T, b.w0, b.x1, b.h + T, b.z0, b.w1);
    add(b.x0, -T, b.z1, b.w0, b.x1, b.h + T, b.z1 + T, b.w1);
    add(b.x0, b.h, b.z0, b.w0, b.x1, b.h + T, b.z1, b.w1);
    for (const [x0, z0, w0, x1, z1, w1] of floorPieces(b)) add(x0, -T, z0, w0, x1, 0, z1, w1);
  }
  const fittings = new Set();
  layers.forEach((L, i) => {
    const w = W_POS[i];
    for (const { x0, y0, z0, x1, y1, z1, opt } of L.solids) {
      if (!opt.building) {
        add(x0, y0, z0, w - EPS, x1, y1, z1, w + EPS);
        continue;
      }
      const key = [x0, y0, z0, x1, y1, z1].join();
      if (fittings.has(key)) continue;
      fittings.add(key);
      const b = buildingOf(w);
      add(x0, y0, z0, b.w0, x1, y1, z1, b.w1);
    }
  });
  return out;
}
// Her hula hoop, leaning against the wall by the door in page Ⅰ.
export const HULA = { x: 3.06, y: 0.37, z: 5.3 };

export const SPAWN = { x: 4.8, z: 4.6, yaw: Math.PI * 0.6 };
export const CAT_ZONE = { x0: 4.2, z0: 3.1, x1: 6.1, z1: 5.3 };
export const STARE_SPOT = { x: 6.25, z: 4.25, yaw: Math.PI / 2 };
// On top of the cabinet over the door, head poking out over the front edge.
export const PERCH = { x: 3.85, y: 2.4, z: 5.74, yaw: Math.PI };
export const DEEP_CAT = { x: 4.0, z: 5.9 };

// The cabinet over the door is a 4D fitting. Its front turns about one hinge line in every page:
// in Ⅰ that part is a small flap door; in Ⅱ it is a long board propped up toward the ceiling,
// further along the same line (dx), beside the cabinet rather than in front of it.
// closed: angle from straight up toward the room; opening adds a quarter turn in every page.
export const HINGE = { x: 3.85, y: 2.0, z: 5.535 };
export const FLAP_PARTS = [
  { page: 0, tex: 'cabpaint0', dx: 0, width: 1.2, length: 0.4, thick: 0.03, closed: 0 },
  { page: 1, tex: 'cabpaint1', dx: 1.1, width: 0.9, length: 2.25, thick: 0.05, closed: (64 * Math.PI) / 180 },
];
// Where the board in Ⅱ comes to rest once lowered: the foot of the ramp.
export const RAMP_FOOT = { x: 4.95, z: 4.62 };
