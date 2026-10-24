import * as THREE from 'three';
import { ConvexHull } from 'three/addons/math/ConvexHull.js';

// Exact 4D geometry for the prologue. Points are [x, y, z, w]; each page is the 3D slice at one w.
// Everything people make is a 3D shape with a tiny thickness EPS either side in W. An object's pose
// is a position P and a rotation M whose four columns are where its local axes point, so the local
// point v sits at P + M v. Every turn in play is a rotation in one plane, and what a page shows at
// any moment is the exact slice of the object by that page.

export const EPS = 0.006;

export const unit = (k) => [0, 1, 2, 3].map((i) => (i === k ? 1 : 0));
export const dot4 = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3];

// Rotate v by angle t in the plane of the orthonormal pair a, b, turning a toward b.
export function rotate(v, a, b, t) {
  const va = dot4(v, a);
  const vb = dot4(v, b);
  const c = Math.cos(t) - 1;
  const s = Math.sin(t);
  return v.map((x, i) => x + c * (va * a[i] + vb * b[i]) + s * (va * b[i] - vb * a[i]));
}

export class Pose {
  constructor(P, M) {
    this.P = P;
    this.M = M;
  }

  // Lying in page w, turned by yaw about the vertical. A mirror image is a half turn through W,
  // which reverses local x and local w together.
  static upright(x, y, z, w, yaw, mirrored) {
    const m = mirrored ? -1 : 1;
    return new Pose([x, y, z, w], [
      [m * Math.cos(yaw), 0, -m * Math.sin(yaw), 0],
      [0, 1, 0, 0],
      [Math.sin(yaw), 0, Math.cos(yaw), 0],
      [0, 0, 0, m],
    ]);
  }

  apply(v) {
    const { P, M } = this;
    return P.map((p, i) => p + v[0] * M[0][i] + v[1] * M[1][i] + v[2] * M[2][i] + v[3] * M[3][i]);
  }

  // The same object turned by t in the plane (a, b) about the point `pivot`.
  turned(a, b, t, pivot) {
    const rel = this.P.map((p, i) => p - pivot[i]);
    return new Pose(
      rotate(rel, a, b, t).map((p, i) => p + pivot[i]),
      this.M.map((col) => rotate(col, a, b, t)),
    );
  }

  // Not leaning into W: the whole object lies in one page.
  get flat() {
    return Math.abs(Math.abs(this.M[3][3]) - 1) < 1e-9;
  }

  // Round off what repeated turns leave behind, so quarter and half turns land exactly.
  clean() {
    for (const col of this.M) col.forEach((x, i) => (col[i] = Math.abs(x) < 1e-9 ? 0 : Math.abs(Math.abs(x) - 1) < 1e-9 ? Math.sign(x) : x));
  }
}

// ---------- slices of box parts ----------

// A part is a box b0..b1 in its own coordinates, placed in the object by `frame` (a THREE.Matrix4),
// EPS thick in W. Cutting the 4D box by the page gives a convex solid whose corners are where the
// box's 32 edges cross the page.
function boxCut(pose, { b0, b1, frame }, c) {
  const corners = [];
  for (let i = 0; i < 16; i++) {
    const p = new THREE.Vector3(i & 1 ? b1.x : b0.x, i & 2 ? b1.y : b0.y, i & 4 ? b1.z : b0.z);
    const q = p.clone().applyMatrix4(frame);
    const X = pose.apply([q.x, q.y, q.z, i & 8 ? EPS : -EPS]);
    corners.push({ X, f: X[3] - c, p });
  }
  const pts = new Map();
  // Points are kept relative to the pose position, each with the spot of the part it came from.
  const add = (X, p) => {
    const v = new THREE.Vector3(X[0] - pose.P[0], X[1] - pose.P[1], X[2] - pose.P[2]);
    v.part = p;
    pts.set(`${v.x.toFixed(7)},${v.y.toFixed(7)},${v.z.toFixed(7)}`, v);
  };
  for (const k of corners) if (Math.abs(k.f) < 1e-12) add(k.X, k.p);
  for (let i = 0; i < 16; i++) {
    for (const bit of [1, 2, 4, 8]) {
      if (i & bit) continue;
      const A = corners[i];
      const B = corners[i | bit];
      if (A.f * B.f >= 0) continue;
      const t = A.f / (A.f - B.f);
      add(A.X.map((x, k) => x + (B.X[k] - x) * t), A.p.clone().lerp(B.p, t));
    }
  }
  return [...pts.values()];
}

// Which side of the box a face lies on, as [axis, ±1]. A cut through the inside is pictured like
// the side it is most nearly parallel to.
function faceSide(ps, { b0, b1 }) {
  for (let k = 0; k < 3; k++) {
    const lo = b0.getComponent(k);
    const hi = b1.getComponent(k);
    const tol = 1e-6 + (hi - lo) * 1e-4;
    if (ps.every((p) => Math.abs(p.getComponent(k) - lo) < tol)) return [k, -1];
    if (ps.every((p) => Math.abs(p.getComponent(k) - hi) < tol)) return [k, 1];
  }
  const n = new THREE.Vector3().subVectors(ps[1], ps[0]).cross(new THREE.Vector3().subVectors(ps[2], ps[0]));
  const a = [Math.abs(n.x), Math.abs(n.y), Math.abs(n.z)];
  const k = a.indexOf(Math.max(...a));
  return [k, Math.sign(n.getComponent(k)) || 1];
}

// Texture coordinates laid out as three's BoxGeometry lays out each side, seen from outside.
function sideUV(p, [k, s], { b0, b1 }) {
  const n = (j) => (p.getComponent(j) - b0.getComponent(j)) / Math.max(b1.getComponent(j) - b0.getComponent(j), 1e-6);
  if (k === 0) return [s > 0 ? 1 - n(2) : n(2), n(1)];
  if (k === 1) return [n(0), s > 0 ? 1 - n(2) : n(2)];
  return [s > 0 ? n(0) : 1 - n(0), n(1)];
}

// The slice of one box part by the page w = c, as a geometry placed relative to the pose position,
// with the slice's centre. Null where the page misses the part.
export function boxSlice(pose, part, c) {
  const pts = boxCut(pose, part, c);
  if (pts.length < 4) return null;
  let hull;
  try {
    hull = new ConvexHull().setFromPoints(pts);
  } catch {
    return null;
  }
  if (hull.faces.length < 4) return null;
  const pos = [];
  const nor = [];
  const uv = [];
  for (const face of hull.faces) {
    const loop = [];
    let e = face.edge;
    do {
      loop.push(e.head().point);
      e = e.next;
    } while (e !== face.edge);
    const side = faceSide(loop.map((v) => v.part), part);
    for (let i = 1; i + 1 < loop.length; i++) {
      for (const v of [loop[0], loop[i], loop[i + 1]]) {
        pos.push(v.x, v.y, v.z);
        nor.push(face.normal.x, face.normal.y, face.normal.z);
        uv.push(...sideUV(v.part, side, part));
      }
    }
  }
  const center = new THREE.Vector3();
  for (const v of pts) center.add(v);
  return { geometry: makeGeometry(pos, nor, uv), center: center.divideScalar(pts.length) };
}

function makeGeometry(pos, nor, uv, index) {
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  g.setAttribute('normal', new THREE.Float32BufferAttribute(nor, 3));
  g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
  g.setAttribute('color', new THREE.Float32BufferAttribute(new Float32Array(pos.length), 3));
  if (index) g.setIndex(index);
  return g;
}

// ---------- slices of a 4D ring ----------

// The ring is everything within `tube` of a circle of radius `radius` lying in the object's local
// y-z plane, centred at local (x, radius + tube, 0, w). Each point of the circle owns a small 3-ball
// of that radius, across the circle's direction; the page cuts each ball in a disc or misses it, and
// the discs together make the slice. Returns the geometry relative to the pose position, the
// ring's centre (also relative) and the direction its opening faces; null where the page misses it.
export function ringSlice(pose, { radius, tube, x, w }, c, seg = 14, sides = 6) {
  const { M } = pose;
  const discs = [];
  for (let i = 0; i < seg; i++) {
    const ph = (i / seg) * Math.PI * 2;
    const rho = M[1].map((m, k) => Math.cos(ph) * m + Math.sin(ph) * M[2][k]);
    const dirs = [rho, M[0], M[3]];
    const g = dirs.map((d) => d[3]);
    const gl = Math.hypot(...g);
    const X = pose.apply([x, radius + tube + radius * Math.cos(ph), radius * Math.sin(ph), w]);
    const delta = gl < 1e-9 ? Infinity : (c - X[3]) / gl;
    if (Math.abs(delta) >= tube) {
      discs.push(null);
      continue;
    }
    const gh = g.map((v) => v / gl);
    const ref = Math.abs(gh[0]) < 0.9 ? [1, 0, 0] : [0, 1, 0];
    const rd = ref[0] * gh[0] + ref[1] * gh[1] + ref[2] * gh[2];
    const p1 = new THREE.Vector3(...ref.map((r, k) => r - rd * gh[k])).normalize();
    const p2 = new THREE.Vector3(...gh).cross(p1);
    const world = (v) => new THREE.Vector3(...[0, 1, 2].map((k) => v.x * dirs[0][k] + v.y * dirs[1][k] + v.z * dirs[2][k]));
    discs.push({
      center: new THREE.Vector3(X[0] - pose.P[0], X[1] - pose.P[1], X[2] - pose.P[2]).addScaledVector(world(new THREE.Vector3(...gh)), delta),
      e1: world(p1),
      e2: world(p2),
      r: Math.sqrt(tube * tube - delta * delta),
    });
  }
  const present = discs.filter(Boolean);
  if (!present.length) return null;

  const pos = [];
  const nor = [];
  const uv = [];
  const index = [];
  const at = [];
  discs.forEach((d, i) => {
    at.push(pos.length / 3);
    if (!d) return;
    for (let j = 0; j <= sides; j++) {
      const ps = (j / sides) * Math.PI * 2;
      const n = d.e1.clone().multiplyScalar(Math.cos(ps)).addScaledVector(d.e2, Math.sin(ps));
      const p = d.center.clone().addScaledVector(n, d.r);
      pos.push(p.x, p.y, p.z);
      nor.push(n.x, n.y, n.z);
      uv.push(i / seg, j / sides);
    }
  });
  const v = (k) => new THREE.Vector3(pos[k * 3], pos[k * 3 + 1], pos[k * 3 + 2]);
  for (let i = 0; i < seg; i++) {
    const i2 = (i + 1) % seg;
    if (!discs[i] || !discs[i2]) continue;
    for (let j = 0; j < sides; j++) {
      const q = [at[i] + j, at[i2] + j, at[i2] + j + 1, at[i] + j + 1];
      // Wind each triangle to face the way the tube's surface faces.
      for (const [a, b, d] of [[q[0], q[1], q[2]], [q[0], q[2], q[3]]]) {
        const face = v(b).sub(v(a)).cross(v(d).sub(v(a)));
        const out = new THREE.Vector3(nor[a * 3], nor[a * 3 + 1], nor[a * 3 + 2]);
        index.push(...(face.dot(out) >= 0 ? [a, b, d] : [a, d, b]));
      }
    }
  }
  const center = new THREE.Vector3();
  for (const d of present) center.add(d.center);
  const axis = new THREE.Vector3(M[1][0], M[1][1], M[1][2]).cross(new THREE.Vector3(M[2][0], M[2][1], M[2][2])).normalize();
  // The middle line of the slice, as segments between neighbouring discs.
  const path = [];
  discs.forEach((d, i) => {
    const next = discs[(i + 1) % seg];
    if (d && next) path.push(d.center, next.center);
  });
  return { geometry: makeGeometry(pos, nor, uv, index), center: center.divideScalar(present.length), axis, path };
}

// ---------- solids and collisions ----------

// A solid is either a 4D box with edges along four directions at right angles (centre c and
// half-edges E) or a 4D ball (centre c and radius r), with its bounds lo..hi for a quick first test.
function bounded(s) {
  const ext = s.E ? [0, 1, 2, 3].map((i) => s.E.reduce((a, e) => a + Math.abs(e[i]), 0)) : [s.r, s.r, s.r, s.r];
  s.lo = s.c.map((v, i) => v - ext[i]);
  s.hi = s.c.map((v, i) => v + ext[i]);
  return s;
}

export function aabbSolid({ x0, y0, z0, w0, x1, y1, z1, w1 }) {
  const h = [(x1 - x0) / 2, (y1 - y0) / 2, (z1 - z0) / 2, (w1 - w0) / 2];
  return bounded({ c: [(x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2, (w0 + w1) / 2], E: h.map((v, i) => unit(i).map((u) => u * v)) });
}

export const ballSolid = (c, r) => bounded({ c, r });

// A box drawn in 3D (centre and three half-edges) lying in page w, EPS thick in W.
export function slabSolid(center, edges, w) {
  return bounded({ c: [center.x, center.y, center.z, w], E: [...edges.map((e) => [e.x, e.y, e.z, 0]), [0, 0, 0, EPS]] });
}

// One box part of an object at a pose.
export function partSolid(pose, { b0, b1, frame }) {
  const at = (x, y, z) => new THREE.Vector3(x, y, z).applyMatrix4(frame);
  const m = b0.clone().add(b1).multiplyScalar(0.5);
  const o = at(m.x, m.y, m.z);
  const half = [at(b1.x, m.y, m.z), at(m.x, b1.y, m.z), at(m.x, m.y, b1.z)].map((p) => p.sub(o));
  const lin = (v) => [0, 1, 2, 3].map((i) => v[0] * pose.M[0][i] + v[1] * pose.M[1][i] + v[2] * pose.M[2][i] + v[3] * pose.M[3][i]);
  return bounded({
    c: pose.apply([o.x, o.y, o.z, 0]),
    E: [...half.map((h) => lin([h.x, h.y, h.z, 0])), lin([0, 0, 0, EPS])],
  });
}

// The direction at right angles to three vectors in 4D.
function cross4(a, b, c) {
  const det3 = (i, j, k) =>
    a[i] * (b[j] * c[k] - b[k] * c[j]) - a[j] * (b[i] * c[k] - b[k] * c[i]) + a[k] * (b[i] * c[j] - b[j] * c[i]);
  return [det3(1, 2, 3), -det3(0, 2, 3), det3(0, 1, 3), -det3(0, 1, 2)];
}

// Two boxes overlap by more than `tol` unless some direction holds them apart. For two such boxes it
// is enough to try the directions at right angles to every three of their eight edges (56 in all,
// every face direction of both among them).
function boxesOverlap(A, B, tol) {
  const G = [...A.E, ...B.E];
  const d = A.c.map((v, i) => B.c[i] - v);
  for (let i = 0; i < 8; i++) {
    for (let j = i + 1; j < 8; j++) {
      for (let k = j + 1; k < 8; k++) {
        const n = cross4(G[i], G[j], G[k]);
        const len = Math.hypot(...n);
        if (len < 1e-12) continue;
        const ra = A.E.reduce((s, e) => s + Math.abs(dot4(e, n)), 0);
        const rb = B.E.reduce((s, e) => s + Math.abs(dot4(e, n)), 0);
        if (Math.abs(dot4(d, n)) >= ra + rb - tol * len) return false;
      }
    }
  }
  return true;
}

// How far a point lies outside a box, measured along the box's own edges.
function boxDistance(A, p) {
  let d2 = 0;
  const rel = p.map((v, i) => v - A.c[i]);
  for (const e of A.E) {
    const len = Math.hypot(...e);
    const out = Math.max(0, Math.abs(dot4(rel, e)) / len - len);
    d2 += out * out;
  }
  return Math.sqrt(d2);
}

// Whether two solids overlap by more than `tol`; things that only touch don't.
export function solidsOverlap(A, B, tol) {
  for (let i = 0; i < 4; i++) if (A.lo[i] > B.hi[i] - tol || B.lo[i] > A.hi[i] - tol) return false;
  if (A.E && B.E) return boxesOverlap(A, B, tol);
  if (A.E) return boxDistance(A, B.c) < B.r - tol;
  if (B.E) return boxDistance(B, A.c) < A.r - tol;
  return Math.hypot(...A.c.map((v, i) => v - B.c[i])) < A.r + B.r - tol;
}

// ---------- resting ----------

// Supports are 4D boxes: a rectangle in x-z, a range in W, and a top at height y. An object comes to
// rest on the highest support under any of its points (given in 4D at its current pose): returns the
// height its position must take.
export function restingY(pose, pts, supports) {
  let up = -Infinity;
  for (const s of supports) {
    for (const X of pts) {
      if (X[0] < s.x0 || X[0] > s.x1 || X[2] < s.z0 || X[2] > s.z1 || X[3] < s.w0 - 1e-9 || X[3] > s.w1 + 1e-9) continue;
      up = Math.max(up, s.y - X[1]);
    }
  }
  return up === -Infinity ? pose.P[1] : pose.P[1] + up;
}

// A 4D ball centred at w touches each support at the point of the support's footprint nearest its
// centre; the further away in W that point is, the lower the ball can sink before touching.
export function ballRestY(x, z, w, R, supports) {
  let y = -Infinity;
  for (const s of supports) {
    const dx = Math.max(s.x0 - x, 0, x - s.x1);
    const dz = Math.max(s.z0 - z, 0, z - s.z1);
    const dw = Math.max(s.w0 - w, 0, w - s.w1);
    const d2 = dx * dx + dz * dz + dw * dw;
    if (d2 < R * R) y = Math.max(y, s.y + Math.sqrt(R * R - d2));
  }
  return y === -Infinity ? R : y;
}
