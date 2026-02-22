# Solar System N-Body Simulation — Project Guide

## Overview

A Unity project that simulates the solar system using GPU-accelerated N-body gravitational physics. Hundreds of thousands of small bodies (asteroids, comets) are simulated on the GPU via a compute shader, while the major solar system bodies (Sun + planets + Pluto + Ceres + Black Hole) are integrated on the CPU. The simulation uses real physical constants, positions in AU, velocities in m/s, and masses in kg.

---

## Architecture

```
BinaryFileLoader (entry point)
  ├── BinaryDataReader         — loads binary .dat files (comets, asteroids)
  ├── BinaryFileLoader         — hard-coded solar system body data, assembles Bodies[] + MajorBodies[]
  └── NBodySimulation          — main simulation controller
        ├── GPU: NBodyVerletIntegration.compute  — Verlet integration for small bodies (dispatched each frame)
        ├── CPU: StepSimulation()                — Verlet integration for major bodies (runs each frame)
        ├── NBodyShader.shader / Comet.shader    — procedural rendering of small bodies
        └── CameraManager / SelectTarget / UI    — camera, tracking, mass/speed sliders
```

### Data Flow (per frame)

1. `NBodySimulation.Update()` uploads `MajorBodies[]` to the GPU buffer.
2. The compute shader (`NBodyVerletIntegration.compute`) runs — each GPU thread integrates one small body against all major bodies using **Velocity Verlet** integration.
3. Updated `MajorBodies[]` collision counters are read back from the GPU.
4. `StepSimulation()` integrates major bodies on the CPU (also Velocity Verlet, all-pairs).
5. Major-body GameObjects' transforms are updated to match the simulation state.
6. `OnRenderObject()` issues a procedural draw call (`Graphics.DrawProceduralNow`) to render all small bodies as points/quads from the GPU buffer.

---

## Key Files

### Simulation Core

| File | Role |
|------|------|
| **NBodySimulation.cs** | Central MonoBehaviour. Owns compute buffers, dispatches the compute shader, runs the CPU major-body integrator, manages UI bindings (speed slider, trail toggle, mass reset). Contains the Barnes–Hut octree builder (currently commented out in `Update`). |
| **NBodyVerletIntegration.compute** | The **active** compute shader. Implements Velocity Verlet integration for small bodies. Each thread loads one body, computes gravitational acceleration from all `majorBodies` (brute-force), updates position and velocity, and detects collisions by comparing distance to an estimated body radius. Collided bodies are flagged and skipped in future frames. |
| **NBodyShader.compute** | An **alternative** compute shader using Euler integration and a Barnes–Hut tree traversal (stack-based octree walk with `theta` opening-angle criterion). Not currently wired up in `Update()`. |

### Data Loading

| File | Role |
|------|------|
| **BinaryFileLoader.cs** | Entry-point MonoBehaviour (`Start` → `Initialize`). Loads binary data files for comets, numbered asteroids, and unnumbered asteroids via `BinaryDataReader`. Also contains `GetSolarSystemBodies()` which returns hard-coded orbital state vectors for the Sun, 8 planets, Pluto, Ceres, and a stellar-mass black hole (index 11). Assembles the `Bodies[]` and `MajorBodies[]` arrays and calls `NBodySimulation.Initialize()`. |
| **BinaryDataReader.cs** | Reads a custom binary format: 4-byte int count, then per body: 3×float position, 3×float velocity, 1×float mass. Validates vectors (no NaN/Infinity). |
| **CoroutineJsonLoader.cs** | Alternative loader that streams a JSON file of `SmallBody` orbital elements using a coroutine. Solves Kepler's equation (Newton-Raphson) and converts Keplerian elements (a, e, i, Ω, ω, M) to Cartesian state vectors (position in AU, velocity in AU/day). |
| **SmallBody.cs** | POCO for deserialized orbital elements: GM, semi-major axis (A), eccentricity (EC), inclination (IN), argument of perihelion (W), longitude of ascending node (OM), mean anomaly (MA), perihelion distance (QR), name. |

### Rendering / Shaders

| File | Role |
|------|------|
| **NBodyShader.shader** | Point-based procedural shader (`Custom/NBodyMaterial`). Vertex shader reads from `StructuredBuffer<Body>` by `SV_VertexID`. Colors bodies by type: comets → light blue, large-mass → yellow glow, asteroids → mass-based gray/rust gradient with inner/outer belt variation. Bodies near the black hole shift toward red/orange (relativistic Doppler). Fragment shader discards (`clip`) collided bodies. Additive blending. |
| **Comet.shader** | Geometry-shader billboard renderer. Expands each body into a camera-facing quad. Differentiates comet vs. non-comet sizes. Skips collided bodies in the geometry stage. Bodies near the black hole are stretched radially toward it (spaghettification). |
| **NBodyQuadShader.shader** | Geometry-shader quad renderer (`Custom/NBody Particles`). Expands points into textured billboard quads sized by mass. Reads from the same `StructuredBuffer`. |

### Camera & UI

| File | Role |
|------|------|
| **CameraManager.cs** | Orbital camera controller. Orbits around `targetObject` with mouse drag rotation, arrow-key rotation, and scroll-wheel zoom. Clamps vertical angle. |
| **SelectTarget.cs** | UI button handler. Each instance has a `targetName` (e.g. "Earth") and a mass slider. Clicking the track button switches the camera target; the slider adjusts that body's mass via `NBodySimulation.SetMass()`. |
| **FollowGameObject.cs** | Positions a `TextMeshProUGUI` label to follow a world-space target, billboard-facing the camera, scaling with distance, hiding beyond `maxShownDistance`. |

### Visual Effects & Motion

| File | Role |
|------|------|
| **BodyRotation.cs** | Spins a planet GameObject around its local up axis with configurable axial tilt and rotation speed, scaled by the simulation time step. |
| **MoonOrbit.cs** | Orbits a moon around its parent (Earth) using `RotateAround`, tidal-locked via `LookAt`. |
| **FixedLengthTrail.cs** | Custom trail renderer using `LineRenderer`. Maintains a fixed-length trail by trimming oldest points when total length exceeds `maxTrailLength`. |
| **BlackHoleEffect.cs** | Post-processing image effect for gravitational lensing and an accretion disk overlay. Two-pass: lensing distortion then disk composite. Wired into the live simulation — reads the black hole's position and mass from `MajorBodies[11]` each frame to drive the screen-space effect. |
| **BlackHoleLensing.shader** | Full-screen post-process shader. Projects the black hole to screen UV, distorts sampling UVs radially (1/r² falloff), and darkens the event horizon region. |
| **AccretionDisk.shader** | Full-screen post-process shader. Composites a procedural spinning disk of hot gas around the black hole's screen position. Additive blend, hotter near the inner edge. |
| **BlackHoleUnlit.shader** | Simple unlit black shader for the black hole's event horizon sphere. |
| **AccretionDiskParticles.cs** | Procedural particle system generating a ring of orbiting hot particles around the black hole prefab. Inner particles orbit faster (Keplerian). |
| **BlackHoleConsumptionCounter.cs** | UI script that reads the black hole's collision counter from `MajorBodies[11].collided` and displays the real-time consumption count. |
| **BlackHoleAbsorptionGlow.cs** | Screen flash effect that pulses orange when the black hole consumes bodies. |

---

## Simulation Details

### Units

| Quantity | Unit |
|----------|------|
| Position | AU (Astronomical Units) |
| Velocity | m/s |
| Mass | kg |
| Time step | seconds (default 5000 s, adjustable via slider) |
| G | 6.67430 × 10⁻¹¹ m³ kg⁻¹ s⁻² |

Conversion factor: **1 AU = 1.496 × 10¹¹ m**. All gravitational calculations are done in SI (meters), then positions are converted back to AU for storage and rendering.

### Integration Methods

- **Small bodies (GPU):** Velocity Verlet — computes acceleration at current position, advances position, recomputes acceleration at new position, averages both for velocity update. Brute-force against `majorBodyCount` major bodies per thread.
- **Major bodies (CPU):** Velocity Verlet — identical scheme, all-pairs N² gravity among the 12 major bodies (Sun, 8 planets, Pluto, Ceres, Black Hole). Runs in `NBodySimulation.StepSimulation()`.

### Collision Detection (GPU)

The compute shader estimates a body radius from mass and density (rock density 5500 kg/m³ for inner planets, gas density 1100 kg/m³ for outer planets). For the black hole (index 11), the Schwarzschild radius $r_s = 2GM/c^2$ is used instead, scaled up by `collisionScale * 1000` for visibility. If a small body's distance to a major body falls below that radius, it is flagged as collided. Collided bodies are skipped in subsequent frames and discarded in the rendering shaders.

### Barnes–Hut Tree (inactive)

`NBodySimulation.cs` contains a full CPU-side octree builder (`RebuildBarnesHutTree` / `BuildTree`) and `NBodyShader.compute` contains a matching GPU tree-traversal kernel. The tree is built by recursively partitioning major bodies into octants and computing center-of-mass aggregates. This path is **currently commented out** — the active compute shader uses brute-force instead. The tree infrastructure remains for future optimization if the number of gravitational sources grows.

---

## Body Struct (shared between C# and HLSL)

```
struct Body {
    float3 position;   // AU
    float3 velocity;   // m/s
    float  mass;       // kg
    float  isComet;    // 1.0 = comet, 0.0 = asteroid
    float  collided;   // -1.0 = active, ≥0 = index of major body it hit
};
```

Stride: 9 × `sizeof(float)` = 36 bytes.

---

## How to Extend

- **Add a new major body:** Add an entry in `BinaryFileLoader.GetSolarSystemBodies()`, add a prefab reference in `NBodySimulation`, and extend `CreateSphere()` / `SetMass()`.
- **Enable Barnes–Hut:** Uncomment `RebuildBarnesHutTree()` in `NBodySimulation.Update()`, switch the compute shader from `NBodyVerletIntegration` to `NBodyShader`, and ensure the `majorNodes` buffer is bound.
- **Change rendering style:** Swap the material assigned to `NBodySimulation.material` between `NBodyShader.shader` (points), `Comet.shader` (billboards), or `NBodyQuadShader.shader` (textured quads).
- **Load different data:** Point `BinaryFileLoader` at different `.dat` files or use `CoroutineJsonLoader` for JSON orbital-element input.
