import * as THREE from 'three';

export const CELL = 0.5;
export const H = 2.8;
export const DOOR_H = 2.1;

const WALK = new Set(['.', ',', ';', 'D']);
// 'B' (junk) blocks movement and light but is drawn as open floor with props on it.
const isWalk = (ch) => WALK.has(ch);
const isOpenVis = (ch) => isWalk(ch) || ch === 'B';

class MeshBuilder {
  constructor() {
    this.p = [];
    this.n = [];
    this.uv = [];
    this.c = [];
  }

  vertex(v, n, u, col) {
    this.p.push(v[0], v[1], v[2]);
    this.n.push(n[0], n[1], n[2]);
    this.uv.push(u[0], u[1]);
    this.c.push(col[0], col[1], col[2]);
  }

  geometry() {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(this.p, 3));
    g.setAttribute('normal', new THREE.Float32BufferAttribute(this.n, 3));
    g.setAttribute('uv', new THREE.Float32BufferAttribute(this.uv, 2));
    g.setAttribute('color', new THREE.Float32BufferAttribute(this.c, 3));
    return g;
  }
}

const add = (a, b, s) => [a[0] + b[0] * s, a[1] + b[1] * s, a[2] + b[2] * s];
const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];

export class Layer {
  constructor(index, def, materials) {
    this.index = index;
    this.def = def;
    this.map = def.map;
    this.materials = materials;
    this.lights = def.lights;
    this.ambient = def.ambient;
    this.H = def.height ?? H;
    this.builders = new Map();
    this.itemBuilders = new Map();
    this.colliders = [];
    this.solids = []; // every box and cylinder drawn, as bounds with the options it was drawn with
    this.items = [];
    this.group = new THREE.Group();
  }

  cell(cx, cz) {
    if (cz < 0 || cz >= this.map.length || cx < 0 || cx >= this.map[0].length) return '#';
    return this.map[cz][cx];
  }

  walkable(x, z) {
    return isWalk(this.cell(Math.floor(x / CELL), Math.floor(z / CELL)));
  }

  // Circle-vs-grid and circle-vs-box test used for both walking and flipping.
  blocked(x, z, r) {
    const c0 = Math.floor((x - r) / CELL);
    const c1 = Math.floor((x + r) / CELL);
    const r0 = Math.floor((z - r) / CELL);
    const r1 = Math.floor((z + r) / CELL);
    for (let cz = r0; cz <= r1; cz++) {
      for (let cx = c0; cx <= c1; cx++) {
        if (isWalk(this.cell(cx, cz))) continue;
        const nx = Math.max(cx * CELL, Math.min(x, (cx + 1) * CELL));
        const nz = Math.max(cz * CELL, Math.min(z, (cz + 1) * CELL));
        if ((nx - x) ** 2 + (nz - z) ** 2 < r * r) return true;
      }
    }
    for (const b of this.colliders) {
      const nx = Math.max(b.x0, Math.min(x, b.x1));
      const nz = Math.max(b.z0, Math.min(z, b.z1));
      if ((nx - x) ** 2 + (nz - z) ** 2 < r * r) return true;
    }
    return false;
  }

  occluded(x0, z0, x1, z1) {
    const d = Math.hypot(x1 - x0, z1 - z0);
    const steps = Math.ceil(d / 0.1);
    for (let i = 1; i < steps; i++) {
      const t = i / steps;
      if (!isWalk(this.cell(Math.floor((x0 + (x1 - x0) * t) / CELL), Math.floor((z0 + (z1 - z0) * t) / CELL)))) return true;
    }
    return false;
  }

  // Baked per-vertex light: room lamps with 2D grid shadows, plus a little grime noise.
  bake(p, n) {
    let r = 0;
    let g = 0;
    let b = 0;
    const sx = p[0] + n[0] * 0.06;
    const sz = p[2] + n[2] * 0.06;
    for (const L of this.lights) {
      const dx = L.x - p[0];
      const dy = L.y - p[1];
      const dz = L.z - p[2];
      const d = Math.hypot(dx, dy, dz);
      if (d > L.range) continue;
      const ndotl = (n[0] * dx + n[1] * dy + n[2] * dz) / Math.max(d, 1e-4);
      if (ndotl <= -0.1) continue;
      if (this.occluded(L.x, L.z, sx, sz)) continue;
      const att = (1 - d / L.range) ** 2 * L.intensity * (0.3 + 0.7 * Math.max(ndotl, 0));
      r += L.color[0] * att;
      g += L.color[1] * att;
      b += L.color[2] * att;
    }
    const h = Math.sin(p[0] * 12.9898 + p[1] * 78.233 + p[2] * 37.719) * 43758.5453;
    const grime = 0.85 + 0.15 * (h - Math.floor(h));
    const floorDark = 0.65 + 0.35 * Math.min(1, p[1] / 0.5);
    const k = grime * floorDark;
    return [r * k, g * k, b * k];
  }

  mb(tex, item) {
    let map = this.builders;
    if (item) {
      if (!this.itemBuilders.has(item)) this.itemBuilders.set(item, new Map());
      map = this.itemBuilders.get(item);
    }
    if (!map.has(tex)) map.set(tex, new MeshBuilder());
    return map.get(tex);
  }

  // A rectangle from origin along uAxis*uLen and vAxis*vLen; the normal is uAxis x vAxis.
  // flip reverses winding so the face is seen from the other side.
  face(tex, o, uAxis, vAxis, uLen, vLen, opt = {}) {
    const m = this.mb(tex, opt.item);
    const flip = !!opt.flip;
    let n = cross(uAxis, vAxis);
    if (flip) n = n.map((v) => -v);
    const su = opt.segU ?? Math.max(1, Math.round(uLen / 0.6));
    const sv = opt.segV ?? Math.max(1, Math.round(vLen / 0.7));
    const [u0, u1] = opt.u ?? [0, 1];
    const [v0, v1] = opt.v ?? [0, 1];
    const lightOf = (p) => (opt.color ? opt.color : this.bake(p, n));
    for (let i = 0; i < su; i++) {
      for (let j = 0; j < sv; j++) {
        const a0 = i / su;
        const a1 = (i + 1) / su;
        const b0 = j / sv;
        const b1 = (j + 1) / sv;
        const P = (a, bb) => add(add(o, uAxis, a * uLen), vAxis, bb * vLen);
        const T = (a, bb) => [u0 + (u1 - u0) * a, v0 + (v1 - v0) * bb];
        const q = [[a0, b0], [a1, b0], [a1, b1], [a0, b1]];
        const order = flip ? [0, 2, 1, 0, 3, 2] : [0, 1, 2, 0, 2, 3];
        for (const k of order) {
          const p = P(q[k][0], q[k][1]);
          m.vertex(p, n, T(q[k][0], q[k][1]), lightOf(p));
        }
      }
    }
  }

  // Axis-aligned box. uv: 'fit' maps each face 0..1, a number tiles the texture every that many meters.
  box(tex, x0, y0, z0, x1, y1, z1, opt = {}) {
    const s = opt.uv ?? 'fit';
    const uvs = (a, b) => (s === 'fit' ? { u: [0, 1], v: [0, 1] } : { u: [0, a / s], v: [0, b / s] });
    const dx = x1 - x0;
    const dy = y1 - y0;
    const dz = z1 - z0;
    const skip = opt.skip ?? '';
    const f = (key, o, u, v, ul, vl) => {
      if (skip.includes(key)) return;
      this.face(tex, o, u, v, ul, vl, { ...uvs(ul, vl), item: opt.item, flip: opt.flip, color: opt.color });
    };
    f('px', [x1, y0, z1], [0, 0, -1], [0, 1, 0], dz, dy);
    f('nx', [x0, y0, z0], [0, 0, 1], [0, 1, 0], dz, dy);
    f('pz', [x0, y0, z1], [1, 0, 0], [0, 1, 0], dx, dy);
    f('nz', [x1, y0, z0], [-1, 0, 0], [0, 1, 0], dx, dy);
    f('py', [x0, y1, z1], [1, 0, 0], [0, 0, -1], dx, dz);
    f('ny', [x0, y0, z0], [1, 0, 0], [0, 0, 1], dx, dz);
    if (opt.solid) this.colliders.push({ x0, z0, x1, z1 });
    this.solids.push({ x0, y0, z0, x1, y1, z1, opt });
  }

  cyl(tex, cx, y0, cz, r0, r1, h, seg = 6, opt = {}) {
    const m = this.mb(tex, opt.item);
    for (let i = 0; i < seg; i++) {
      const a0 = (i / seg) * Math.PI * 2;
      const a1 = ((i + 1) / seg) * Math.PI * 2;
      const pts = [
        [cx + Math.cos(a0) * r0, y0, cz + Math.sin(a0) * r0],
        [cx + Math.cos(a1) * r0, y0, cz + Math.sin(a1) * r0],
        [cx + Math.cos(a1) * r1, y0 + h, cz + Math.sin(a1) * r1],
        [cx + Math.cos(a0) * r1, y0 + h, cz + Math.sin(a0) * r1],
      ];
      const am = (a0 + a1) / 2;
      const n = [Math.cos(am), 0, Math.sin(am)];
      const uv = [[i / seg, 0], [(i + 1) / seg, 0], [(i + 1) / seg, 1], [i / seg, 1]];
      for (const k of [0, 2, 1, 0, 3, 2]) m.vertex(pts[k], n, uv[k], opt.color ?? this.bake(pts[k], n));
      if (!opt.open) {
        const top = [cx, y0 + h, cz];
        for (const p of [top, pts[2], pts[3]]) m.vertex(p, [0, 1, 0], [0.5, 0.5], opt.color ?? this.bake(p, [0, 1, 0]));
      }
    }
    if (opt.solid) this.colliders.push({ x0: cx - r0, z0: cz - r0, x1: cx + r0, z1: cz + r0 });
    const r = Math.max(r0, r1);
    this.solids.push({ x0: cx - r, y0, z0: cz - r, x1: cx + r, y1: y0 + h, z1: cz + r, opt });
  }

  // A flat picture hung on a wall or laid on a surface. facing: '+x' | '-x' | '+z' | '-z' | '+y'.
  decal(tex, cx, cy, cz, facing, w, h, opt = {}) {
    const e = 0.008;
    const hw = w / 2;
    const hh = h / 2;
    const o = { item: opt.item, segU: 1, segV: 1, color: opt.color };
    if (facing === '+x') this.face(tex, [cx + e, cy - hh, cz + hw], [0, 0, -1], [0, 1, 0], w, h, o);
    if (facing === '-x') this.face(tex, [cx - e, cy - hh, cz - hw], [0, 0, 1], [0, 1, 0], w, h, o);
    if (facing === '+z') this.face(tex, [cx - hw, cy - hh, cz + e], [1, 0, 0], [0, 1, 0], w, h, o);
    if (facing === '-z') this.face(tex, [cx + hw, cy - hh, cz - e], [-1, 0, 0], [0, 1, 0], w, h, o);
    if (facing === '+y') this.face(tex, [cx - hw, cy + e, cz + hh], [1, 0, 0], [0, 0, -1], w, h, o);
  }

  item(spec) {
    this.items.push({ layer: this.index, y: 0.8, ...spec });
  }

  buildShell() {
    const L = this.def.tex;
    const rows = this.map.length;
    const cols = this.map[0].length;
    const dirs = [
      [1, 0, 'px'],
      [-1, 0, 'nx'],
      [0, 1, 'pz'],
      [0, -1, 'nz'],
    ];
    for (let cz = 0; cz < rows; cz++) {
      for (let cx = 0; cx < cols; cx++) {
        const ch = this.cell(cx, cz);
        const x0 = cx * CELL;
        const x1 = x0 + CELL;
        const z0 = cz * CELL;
        const z1 = z0 + CELL;
        if (isOpenVis(ch)) {
          const floorTex = { ',': L.wood, ';': L.tile }[ch] ?? L.floor;
          this.face(floorTex, [x0, 0, z1], [1, 0, 0], [0, 0, -1], CELL, CELL, { u: [x0, x1], v: [-z1, -z0], segU: 1, segV: 1 });
          const top = ch === 'D' ? DOOR_H : this.H;
          this.face(L.ceiling, [x0, top, z0], [1, 0, 0], [0, 0, 1], CELL, CELL, { u: [x0, x1], v: [z0, z1], segU: 1, segV: 1 });
          if (ch === 'D') {
            // Lintel faces above the doorway, toward neighbours that have a full-height ceiling.
            for (const [dx, dz] of dirs) {
              const nch = this.cell(cx + dx, cz + dz);
              if (!isOpenVis(nch) || nch === 'D') continue;
              this.wallFace(L.wall, cx, cz, dx, dz, DOOR_H, this.H);
            }
          }
          continue;
        }
        const tex = ch === 'M' ? 'flesh' : L.wall;
        for (const [dx, dz] of dirs) {
          if (isOpenVis(this.cell(cx + dx, cz + dz))) this.wallFace(tex, cx, cz, dx, dz, 0, this.H);
        }
      }
    }
  }

  // Vertical face on the side (dx,dz) of cell (cx,cz), facing outward from that cell.
  wallFace(tex, cx, cz, dx, dz, y0, y1) {
    const x0 = cx * CELL;
    const x1 = x0 + CELL;
    const z0 = cz * CELL;
    const z1 = z0 + CELL;
    let o;
    let u;
    if (dx === 1) { o = [x1, y0, z1]; u = [0, 0, -1]; }
    if (dx === -1) { o = [x0, y0, z0]; u = [0, 0, 1]; }
    if (dz === 1) { o = [x0, y0, z1]; u = [1, 0, 0]; }
    if (dz === -1) { o = [x1, y0, z0]; u = [-1, 0, 0]; }
    const along = o[0] * u[0] + o[2] * u[2];
    this.face(tex, o, u, [0, 1, 0], CELL, y1 - y0, {
      u: [along, along + CELL],
      v: [y0 / this.H, y1 / this.H],
      segU: 1,
      segV: Math.max(1, Math.round((y1 - y0) / 0.6)),
    });
  }

  lamps() {
    for (const L of this.lights) {
      if (!L.fixture) continue;
      if (L.fixture === 'bulb') {
        this.box('black', L.x - 0.008, L.y + 0.05, L.z - 0.008, L.x + 0.008, this.H, L.z + 0.008, { color: [0.05, 0.05, 0.05] });
        this.box('bulb', L.x - 0.045, L.y - 0.05, L.z - 0.045, L.x + 0.045, L.y + 0.05, L.z + 0.045, { color: [0, 0, 0] });
      } else if (L.fixture === 'tube') {
        this.box('tube', L.x - 0.6, L.y - 0.02, L.z - 0.03, L.x + 0.6, L.y + 0.02, L.z + 0.03, { color: [0, 0, 0] });
      }
    }
  }

  build() {
    this.buildShell();
    this.lamps();
    this.def.props(this);
    const addMeshes = (map, parent) => {
      for (const [tex, b] of map) {
        const mesh = new THREE.Mesh(b.geometry(), this.materials(tex));
        parent.add(mesh);
      }
    };
    addMeshes(this.builders, this.group);
    for (const it of this.items) {
      if (!this.itemBuilders.has(it.id)) continue;
      it.group = new THREE.Group();
      addMeshes(this.itemBuilders.get(it.id), it.group);
      this.group.add(it.group);
    }
  }
}
