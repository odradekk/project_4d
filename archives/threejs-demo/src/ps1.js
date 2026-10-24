import * as THREE from 'three';

// Internal render height. The scene is drawn at this resolution and scaled up with nearest sampling.
export const LOW_H = 240;
export const MAX_BITES = 24;

// Uniforms shared by every world material; one update per render pass reaches all of them.
export const U = {
  uSnap: { value: new THREE.Vector2(160, 120) },
  uFogColor: { value: new THREE.Color(0, 0, 0) },
  uFogNear: { value: 2 },
  uFogFar: { value: 12 },
  uLightPos: { value: new THREE.Vector3() },
  uLightColor: { value: new THREE.Color(0.85, 0.72, 0.52) },
  uLightRange: { value: 4 },
  uAmbient: { value: new THREE.Color(0.05, 0.05, 0.05) },
  uGlobal: { value: 1 },
  uAffine: { value: 0.7 },
  uTime: { value: 0 },
  // Boxes of the world that have been eaten away: fragments inside any of them are discarded.
  uBiteCount: { value: 0 },
  uBites: { value: Array.from({ length: MAX_BITES }, () => new THREE.Vector4()) },
  uBiteSize: { value: Array.from({ length: MAX_BITES }, () => new THREE.Vector4()) },
};

// Snap clip-space xy to the low-res pixel grid, like the PS1 GTE's integer screen coordinates.
const snapChunk = /* glsl */ `
  if (clip.w > 0.0) {
    vec2 ndc = clip.xy / clip.w;
    ndc = floor(ndc * uSnap + 0.5) / uSnap;
    clip.xy = ndc * clip.w;
  }
`;

const worldVert = /* glsl */ `
  uniform vec2 uSnap;
  uniform vec3 uLightPos;
  uniform vec3 uLightColor;
  uniform float uLightRange;
  uniform vec3 uAmbient;
  uniform float uGlobal;
  uniform float uEmissive;
  uniform float uFogNear;
  uniform float uFogFar;
  varying vec2 vUv;
  varying vec3 vUvw;
  varying vec3 vLight;
  varying float vFog;
  varying vec3 vWorldPos;
  void main() {
    vec4 world = modelMatrix * vec4(position, 1.0);
    vWorldPos = world.xyz;
    vec3 n = normalize(mat3(modelMatrix) * normal);
    vec3 toL = uLightPos - world.xyz;
    float d = length(toL);
    float att = clamp(1.0 - d / uLightRange, 0.0, 1.0);
    att *= att;
    float lam = max(dot(n, toL / max(d, 1e-4)), 0.0) * 0.75 + 0.25;
    // Gouraud lighting: baked room light in the vertex color plus the player's lamp.
    vLight = uAmbient + (color + vec3(uEmissive)) * uGlobal + uLightColor * att * lam;
    vec4 mv = viewMatrix * world;
    vec4 clip = projectionMatrix * mv;
    ${snapChunk}
    gl_Position = clip;
    vUv = uv;
    vUvw = vec3(uv * clip.w, clip.w);
    vFog = clamp((length(mv.xyz) - uFogNear) / (uFogFar - uFogNear), 0.0, 1.0);
  }
`;

const worldFrag = /* glsl */ `
  uniform sampler2D map;
  uniform vec3 uFogColor;
  uniform float uAlphaTest;
  uniform float uAffine;
  uniform int uBiteCount;
  uniform vec4 uBites[${MAX_BITES}];
  uniform vec4 uBiteSize[${MAX_BITES}];
  varying vec2 vUv;
  varying vec3 vUvw;
  varying vec3 vLight;
  varying float vFog;
  varying vec3 vWorldPos;
  void main() {
    // Bites are boxes turned by w around the vertical axis; whatever falls inside is simply gone.
    for (int i = 0; i < ${MAX_BITES}; i++) {
      if (i >= uBiteCount) break;
      vec3 p = vWorldPos - uBites[i].xyz;
      float c = cos(uBites[i].w);
      float s = sin(uBites[i].w);
      vec2 r = vec2(c * p.x - s * p.z, s * p.x + c * p.z);
      if (abs(r.x) < uBiteSize[i].x && abs(p.y) < uBiteSize[i].y && abs(r.y) < uBiteSize[i].z) discard;
    }
    // uv*w / w undoes perspective correction, giving the PS1's affine texture warp.
    vec2 uv = mix(vUv, vUvw.xy / vUvw.z, uAffine);
    vec4 tex = texture2D(map, uv);
    if (tex.a < uAlphaTest) discard;
    vec3 col = tex.rgb * vLight;
    gl_FragColor = vec4(mix(col, uFogColor, vFog), 1.0);
  }
`;

export function ps1Material(map, { emissive = 0, alphaTest = 0, side = THREE.FrontSide } = {}) {
  return new THREE.ShaderMaterial({
    uniforms: { ...U, map: { value: map }, uEmissive: { value: emissive }, uAlphaTest: { value: alphaTest } },
    vertexShader: worldVert,
    fragmentShader: worldFrag,
    vertexColors: true,
    side,
  });
}

const creatureVert = /* glsl */ `
  uniform vec2 uSnap;
  uniform float uTime;
  uniform float uFogNear;
  uniform float uFogFar;
  varying vec3 vWorld;
  varying float vFog;
  void main() {
    vec3 p = position;
    p += 0.018 * vec3(sin(uTime * 7.0 + p.y * 13.0), sin(uTime * 5.0 + p.x * 11.0), sin(uTime * 6.0 + p.z * 12.0));
    vec4 world = modelMatrix * vec4(p, 1.0);
    vWorld = world.xyz;
    vec4 mv = viewMatrix * world;
    vec4 clip = projectionMatrix * mv;
    ${snapChunk}
    gl_Position = clip;
    vFog = clamp((length(mv.xyz) - uFogNear) / (uFogFar - uFogNear), 0.0, 1.0);
  }
`;

const creatureFrag = /* glsl */ `
  uniform vec3 uFogColor;
  uniform vec3 uLightPos;
  uniform vec3 uLightColor;
  uniform float uLightRange;
  uniform vec3 uAmbient;
  uniform float uGlobal;
  uniform vec3 uTint;
  uniform float uSelfLight;
  uniform vec4 uThroat;    // xyz: centre of a mouth opening, w: 1 to enable
  uniform vec3 uThroatDir; // which way the mouth faces
  varying vec3 vWorld;
  varying float vFog;
  void main() {
    // Flat normal from screen derivatives: faceted shading without caring about triangle winding.
    vec3 n = normalize(cross(dFdx(vWorld), dFdy(vWorld)));
    vec3 v = normalize(cameraPosition - vWorld);
    if (dot(n, v) < 0.0) n = -n;
    vec3 toL = uLightPos - vWorld;
    float d = length(toL);
    float att = clamp(1.0 - d / uLightRange, 0.0, 1.0);
    att *= att;
    float lam = max(dot(n, toL / max(d, 1e-4)), 0.0);
    float rim = pow(1.0 - max(dot(n, v), 0.0), 2.0);
    float m = fract(sin(dot(floor(vWorld * 10.0), vec3(12.9898, 78.233, 37.719))) * 43758.5453);
    vec3 col = uTint * (uAmbient * 3.5 * uGlobal + vec3(uSelfLight) + uLightColor * att * (0.25 + 0.9 * lam));
    float spec = pow(max(dot(reflect(-toL / max(d, 1e-4), n), v), 0.0), 12.0) * att;
    col = col * (0.82 + 0.3 * m) + rim * 0.1 * uTint + spec * 0.35 * uLightColor;
    if (uThroat.w > 0.5) {
      // Darken whatever lies inside the rim and behind the mouth: reads as a throat.
      vec3 rel = vWorld - uThroat.xyz;
      float depth = -dot(rel, uThroatDir);
      float radial = length(rel + uThroatDir * depth);
      col *= 1.0 - 0.9 * smoothstep(0.1, 0.5, depth) * (1.0 - smoothstep(0.8, 1.2, radial));
    }
    gl_FragColor = vec4(mix(col, uFogColor, vFog), 1.0);
  }
`;

export function creatureMaterial(tint = [0.86, 0.66, 0.6], selfLight = 0.05) {
  return new THREE.ShaderMaterial({
    uniforms: {
      ...U,
      uTint: { value: new THREE.Color(...tint) },
      uSelfLight: { value: selfLight },
      uThroat: { value: new THREE.Vector4() },
      uThroatDir: { value: new THREE.Vector3(0, 0, 1) },
    },
    vertexShader: creatureVert,
    fragmentShader: creatureFrag,
    side: THREE.DoubleSide,
  });
}

const postVert = /* glsl */ `
  varying vec2 vUv;
  void main() { vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }
`;

const postFrag = /* glsl */ `
  uniform sampler2D tMain;
  uniform sampler2D tPeek;
  uniform vec2 uLowRes;
  uniform float uAspect;
  uniform float uPeek;
  uniform float uMirror;
  uniform float uSqueeze;
  uniform float uTime;
  uniform float uNoise;
  uniform float uWarp;
  uniform float uDamage;
  uniform float uDesat;
  uniform float uFade;
  uniform float uLensNoise;
  varying vec2 vUv;

  float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
  float bayer4(vec2 p) {
    ivec2 i = ivec2(mod(p, 4.0));
    const float m[16] = float[16](0., 8., 2., 10., 12., 4., 14., 6., 3., 11., 1., 9., 15., 7., 13., 5.);
    return m[i.x + i.y * 4] / 16.0;
  }

  void main() {
    vec2 uv = vUv;
    uv.x = (uv.x - 0.5) / max(uSqueeze, 0.001) + 0.5;
    if (uv.x < 0.0 || uv.x > 1.0) { gl_FragColor = vec4(0.0, 0.0, 0.0, 1.0); return; }
    if (uMirror > 0.5) uv.x = 1.0 - uv.x;
    uv.x += sin(uv.y * 38.0 + uTime * 5.0) * 0.003 * uWarp;

    vec2 px = floor(uv * uLowRes);
    vec2 suv = (px + 0.5) / uLowRes;
    vec3 col = texture2D(tMain, suv).rgb;

    if (uPeek > 0.001) {
      vec2 p = (suv - 0.5) * vec2(uAspect, 1.0);
      float r = length(p);
      float R = mix(0.08, 0.4, uPeek);
      if (r < R) {
        // Pages far away in W come through the lens jittery and full of snow.
        vec2 j = (vec2(hash(px + uTime * 13.0), hash(px.yx + uTime * 7.0)) - 0.5) * uLensNoise * 0.025;
        vec3 pc = texture2D(tPeek, suv + j).rgb;
        pc = mix(pc, vec3(hash(px + fract(uTime) * 57.0)) * 0.6, uLensNoise * 0.55);
        float l = dot(pc, vec3(0.299, 0.587, 0.114));
        pc = mix(pc, l * vec3(1.1, 0.96, 0.78), 0.45) * 1.15;
        pc *= mix(0.35, 1.0, smoothstep(R, R * 0.55, r));
        col = pc;
      } else if (r < R + 0.022) {
        float a = atan(p.y, p.x);
        col = vec3(0.32, 0.25, 0.12) * (0.65 + 0.35 * step(0.5, fract(a * 9.55)));
      } else {
        col *= mix(1.0, 0.18, uPeek);
      }
    }

    float l = dot(col, vec3(0.299, 0.587, 0.114));
    col = mix(col, vec3(l), uDesat);
    col = mix(col, vec3(l * 1.2, l * 0.1, l * 0.05) + vec3(0.12, 0.0, 0.0), uDamage * 0.7);
    vec2 c = vUv - 0.5;
    col *= 1.0 - dot(c, c) * 1.1;
    col += (hash(px + fract(uTime) * 91.0) - 0.5) * (0.035 + uNoise * 0.22);
    col *= 1.0 - uFade;

    // 15-bit color with ordered dithering, applied in low-res pixel space.
    col += (bayer4(px) - 0.5) / 31.0;
    col = floor(clamp(col, 0.0, 1.0) * 31.0 + 0.5) / 31.0;
    gl_FragColor = vec4(col, 1.0);
  }
`;

export class Pipeline {
  constructor(renderer) {
    this.renderer = renderer;
    const opts = { minFilter: THREE.NearestFilter, magFilter: THREE.NearestFilter, generateMipmaps: false };
    this.rtMain = new THREE.WebGLRenderTarget(4, 4, opts);
    this.rtPeek = new THREE.WebGLRenderTarget(4, 4, opts);
    this.post = new THREE.ShaderMaterial({
      uniforms: {
        tMain: { value: this.rtMain.texture },
        tPeek: { value: this.rtPeek.texture },
        uLowRes: { value: new THREE.Vector2(4, 4) },
        uAspect: { value: 1 },
        uPeek: { value: 0 },
        uMirror: { value: 0 },
        uSqueeze: { value: 1 },
        uTime: { value: 0 },
        uNoise: { value: 0 },
        uWarp: { value: 0 },
        uDamage: { value: 0 },
        uDesat: { value: 0 },
        uFade: { value: 0 },
        uLensNoise: { value: 0 },
      },
      vertexShader: postVert,
      fragmentShader: postFrag,
      depthTest: false,
      depthWrite: false,
    });
    this.postScene = new THREE.Scene();
    this.postScene.add(new THREE.Mesh(new THREE.PlaneGeometry(2, 2), this.post));
    this.postCam = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
  }

  resize(width, height) {
    // A hidden window can report zero size; a zero-sized target makes every draw call fail.
    const aspect = Math.max(width, 1) / Math.max(height, 1);
    const h = LOW_H;
    const w = Math.max(1, Math.round(h * aspect));
    this.rtMain.setSize(w, h);
    this.rtPeek.setSize(w, h);
    this.post.uniforms.uLowRes.value.set(w, h);
    this.post.uniforms.uAspect.value = aspect;
    // Snap to every other low-res pixel: a little coarser than one pixel reads better at this size.
    U.uSnap.value.set(w / 4, h / 4);
  }

  renderTo(target, scene, camera, clearColor) {
    const r = this.renderer;
    r.setRenderTarget(target);
    r.setClearColor(clearColor, 1);
    r.clear();
    r.render(scene, camera);
  }

  present() {
    const r = this.renderer;
    r.setRenderTarget(null);
    r.render(this.postScene, this.postCam);
  }
}
