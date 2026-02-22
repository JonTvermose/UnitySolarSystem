# Black Hole Feature — Implementation Plan

## Concept

Add a black hole as a new gravitational major body in the simulation. It should be fully integrated into the N-body physics (GPU + CPU), have a unique visual representation (event horizon, gravitational lensing, accretion disk), and be controllable via the existing UI pattern (track button, mass slider). The existing `BlackHoleEffect.cs` provides a starting point for lensing/disk visuals but needs to be wired into the live simulation.

---

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Major body index** | 11 (after Ceres at 10) | Follows existing convention |
| **Default position** | Configurable — e.g. (0, 0, -15) AU or outside the solar system | Far enough to not immediately disrupt orbits |
| **Default mass** | ~4.0 × 10³⁰ kg (~2× Sun) | Realistic stellar black hole; tunable via slider |
| **Schwarzschild radius** | Computed from mass: $r_s = \frac{2GM}{c^2}$ | For visual event horizon sizing; ~6 km real scale, scaled up for visibility |
| **Collision behavior** | Bodies within event horizon radius are consumed | Same pattern as planet collisions but with a special "black hole density" |
| **Visual approach** | Dark sphere + `BlackHoleEffect.cs` lensing + accretion disk | Leverages existing post-processing code |
| **Spawning** | Toggle button in UI, or always present at start | UI button allows enabling/disabling the feature |

---

## Task List

### Phase 1: Physics Integration (Core)

- [ ] **1.1** Add black hole entry to `BinaryFileLoader.GetSolarSystemBodies()` at index 11 with position, velocity (e.g. approaching the solar system), and mass
- [ ] **1.2** Add `public GameObject blackHole;` prefab reference in `NBodySimulation.cs` alongside the other planet references
- [ ] **1.3** Add `if (i == 11)` branch in `CreateSphere()` to instantiate the black hole prefab with a unique name `"BlackHole"`
- [ ] **1.4** Add `"BlackHole"` case to `SetMass()` switch statement mapping to `MajorBodies[11]`
- [ ] **1.5** Update the compute shader collision detection density logic — the black hole needs its own density category (or just use Schwarzschild radius directly instead of mass/density):
  ```hlsl
  float majorRadius;
  if (i == 11) {
      // Schwarzschild radius: r_s = 2GM/c^2  (but scaled up for visibility)
      majorRadius = (2.0 * G * major.mass) / (299792458.0 * 299792458.0);
      majorRadius *= collisionScale * 1000.0; // scale up so it's visible
  } else {
      // existing density-based radius
  }
  ```
- [ ] **1.6** Verify the black hole participates in CPU `StepSimulation()` — it will automatically since it's in `MajorBodies[]`, but confirm the all-pairs gravity loop includes index 11

### Phase 2: Black Hole Prefab & Basic Visual

- [ ] **2.1** Create a black hole prefab in Unity:
  - A sphere with a fully black, unlit material (no light response)
  - Scaled appropriately for the scene (e.g. 0.03–0.05 AU display radius)
  - Tag it as `"TrackingObject"` so `SelectTarget` can find it
- [ ] **2.2** Add a subtle particle effect halo around the prefab — a ring of orbiting hot particles (orange/white) to suggest an accretion disk in 3D, visible from all angles
- [ ] **2.3** Optionally attach a `BodyRotation` component to spin the accretion disk effect

### Phase 3: Gravitational Lensing Post-Processing

- [ ] **3.1** Create `BlackHoleLensing.shader` — a full-screen image effect that distorts UVs around the black hole's screen-space position:
  ```
  - Project black hole world position to screen UV
  - For each pixel, compute direction toward BH in screen space
  - Offset the sample UV radially, strength falling off as 1/r²
  - Darken pixels inside the event horizon radius to black
  ```
- [ ] **3.2** Create `AccretionDisk.shader` — composites a spinning disk overlay:
  ```
  - Compute pixel angle relative to BH screen position
  - Sample a procedural ring pattern (inner/outer radius)
  - Rotate ring UVs by time-based angle
  - Additive blend hot orange/white color, fade with distance from BH
  ```
- [ ] **3.3** Wire `BlackHoleEffect.cs` into the live simulation:
  - Each frame, set `blackHoleWorldPos` from `_majorBodies[11].transform.position`
  - Set `blackHoleMass` from `MajorBodies[11].mass`
  - Compute display-space event horizon radius from mass
  - Enable/disable the effect based on whether the black hole is in view (camera frustum check)
- [ ] **3.4** Assign the two shader materials to `BlackHoleEffect.cs` in the inspector

### Phase 4: Small Body Interactions (GPU)

- [ ] **4.1** Update `NBodyShader.shader` (`GetBodyColor`) — bodies near the black hole could shift toward red/blue to simulate relativistic Doppler (optional, decorative):
  ```hlsl
  float distToBH = length(body.position - majorBodies[11].position);
  if (distToBH < someThreshold) {
      color = lerp(color, float3(1, 0.3, 0), saturate(1.0 - distToBH / someThreshold));
  }
  ```
- [ ] **4.2** Consider adding an "absorption glow" — when small bodies collide with the black hole, emit a brief flash. This could be tracked via the `majorBodies[11].collided` counter (collision count) and trigger a particle burst or screen flash on the CPU side
- [ ] **4.3** Optional: Spaghettification visual — bodies very close to the BH but not yet consumed could have their point/quad stretched radially toward it in the geometry shader

### Phase 5: UI Integration

- [ ] **5.1** Add a "Black Hole" track button in the UI panel (same pattern as other planets via `SelectTarget` with `targetName = "BlackHole"`)
- [ ] **5.2** Add a mass slider for the black hole (same pattern, wired to `SetMass("BlackHole")`)
- [ ] **5.3** Add a UI toggle to spawn/despawn the black hole:
  - Spawning: insert into `MajorBodies[]` at index 11, resize buffers, reinitialize
  - Despawning: remove from array, resize buffers, reinitialize
  - Alternative (simpler): always present but mass can be set to 0 to "disable"
- [ ] **5.4** Add a UI label (`FollowGameObject`) that follows the black hole and shows its name + collision count
- [ ] **5.5** Display the number of bodies consumed in real-time (read from `majorBodies[11].collided` count)

### Phase 6: Polish & Edge Cases

- [ ] **6.1** Handle time-step sensitivity — a very massive black hole with large `dt` can cause bodies to "tunnel" through the event horizon. Consider reducing effective dt for BH interactions or using a sub-stepping approach
- [ ] **6.2** Clamp the mass slider to a reasonable range — too high and the black hole will fling all planets out of the solar system in one frame
- [ ] **6.3** Add trail rendering for the black hole's path (same `FixedLengthTrail` as planets, perhaps with a different color — dark purple or red)
- [ ] **6.4** Test with the black hole approaching from outside the solar system at various velocities to find visually interesting scenarios
- [ ] **6.5** Ensure `ResetMass()` and reset button correctly handle the black hole entry at index 11
- [ ] **6.6** Add the black hole to the `agents.md` documentation

---

## Files Modified

| File | Changes |
|------|---------|
| `BinaryFileLoader.cs` | Add BH body at index 11 in `GetSolarSystemBodies()` |
| `NBodySimulation.cs` | Add `blackHole` prefab ref, `CreateSphere` branch, `SetMass` case |
| `NBodyVerletIntegration.compute` | Add Schwarzschild radius logic for BH collision index |
| `NBodyShader.shader` | Optional: color shift near BH |
| `Comet.shader` | Optional: spaghettification stretch |
| `BlackHoleEffect.cs` | Wire to live simulation data, update each frame |
| **New: `BlackHoleLensing.shader`** | Full-screen UV distortion shader |
| **New: `AccretionDisk.shader`** | Spinning disk composite shader |
| `SelectTarget.cs` | No changes needed (data-driven via `targetName`) |

---

## Suggested Starting State

```csharp
// In BinaryFileLoader.GetSolarSystemBodies(), after Ceres:
bodies.Add(new Body
{
    position = new Vector3(0, 2.0f, -15.0f),    // AU — approaching from below/behind
    velocity = new Vector3(0, -500.0f, 3000.0f), // m/s — slow approach toward inner system
    mass = 4.0e30f,                               // ~2× solar mass
    collided = 0
}); // Black Hole
```

This places the black hole ~15 AU behind the solar system, drifting inward slowly — giving the user time to observe its gravitational influence building before close approach.

---

## Milestones

| Milestone | Tasks | Result |
|-----------|-------|--------|
| **M1 — Gravity works** | 1.1–1.6 | Black hole pulls on everything, planets respond, small bodies get consumed |
| **M2 — Visible in scene** | 2.1–2.3 | Dark sphere with basic accretion glow, trackable by camera |
| **M3 — Lensing effect** | 3.1–3.4 | Background stars/bodies distort around the BH on screen |
| **M4 — Full UI** | 5.1–5.5 | Track, mass slider, spawn toggle, consumption counter |
| **M5 — Polished** | 4.1–4.3, 6.1–6.6 | Color effects, spaghettification, edge-case handling |
