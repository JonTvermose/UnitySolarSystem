# Code Analysis — Bugs, Errors & Performance Optimizations

---

## Bugs & Errors

### 1. `SetMass()` — Mercury uses `*=` instead of `=` (NBodySimulation.cs, line ~248)

```csharp
case "Mercury":
    MajorBodies[1].mass *= normalMassBodies[1].mass * massMultiplier;
    break;
```

Every other planet uses `=` (assignment), but Mercury uses `*=` (multiply-assign). This compounds the mass on every slider change instead of setting it to `baseMass × multiplier`. Should be:

```csharp
MajorBodies[1].mass = normalMassBodies[1].mass * massMultiplier;
```

---

### 2. `ToggleTrail()` — Logic error with nullable bool (NBodySimulation.cs, line ~310)

```csharp
trail.time = trail.time == 0 && !(forceDisable == null && forceDisable.Value) ? 15 : 0;
```

The condition `forceDisable == null && forceDisable.Value` will **always be false** — if `forceDisable` is `null`, accessing `.Value` would throw, but `&&` short-circuits. The intent was likely `forceDisable != null && forceDisable.Value` (or simply `forceDisable == true`). Current behavior: trails always toggle off instead of toggling on/off.

Similarly a few lines above:

```csharp
else if(forceDisable == null || !forceDisable.Value)
```

When `forceDisable` is `null`, `!forceDisable.Value` throws `InvalidOperationException`. Should be:

```csharp
else if (forceDisable is null or { Value: false })
```

---

### 3. `CoroutineJsonLoader.ProcessData()` — GM parse logic is inverted (CoroutineJsonLoader.cs, line ~70)

```csharp
if (float.TryParse(data.GM, out var gmParsed))
{
    gmParsed = 0.0f;   // ← overwrites the successfully parsed value!
}
```

When parsing succeeds, the result is thrown away and set to 0. When parsing fails, `gmParsed` defaults to 0. So **mass is always 0** for all loaded comets. The fix should be:

```csharp
if (!float.TryParse(data.GM, out var gmParsed))
{
    gmParsed = 0.0f;
}
```

---

### 4. `NBodyShader.shader` — Duplicate unreachable condition (NBodyShader.shader, lines ~67-73)

```hlsl
if (body.mass > 10000000000000.0)
{
    return float3(1.0, 0.9, 0.2);       // ← this branch is always taken first
}
if (body.mass > 10000000000000.0)        // ← identical condition, never reached
{
    return float3(1.5, 1.3, 0.0) * 2.0;
}
```

The second `if` has the same threshold so it's dead code. Likely one was intended to be a higher threshold (e.g. `10000000000000000.0`) to distinguish very large bodies from moderately large ones.

---

### 5. Compute buffer not resized on re-initialize (NBodySimulation.cs, line ~108)

```csharp
if (bodyBuffer == null)
{
    bodyBuffer = new ComputeBuffer(Bodies.Length, bodyStride);
}
bodyBuffer.SetData(Bodies);
```

If `Initialize()` is called again (e.g. via the reset button) but `Bodies.Length` has changed, the existing buffer keeps its old size. `SetData` will either crash or silently corrupt if the new array is larger. The buffer should be released and recreated if the count doesn't match.

---

### 6. Compute shader out-of-bounds dispatch (NBodyVerletIntegration.compute)

The dispatch launches `ceil(Bodies.Length / 256)` thread groups, meaning up to 255 extra threads may execute beyond the array. The shader has no bounds check:

```hlsl
void Simulate(uint id : SV_DispatchThreadID)
{
    Body body = bodies[id];  // ← id may exceed body count
```

Reading past the buffer end is undefined behavior on GPU. A guard is needed:

```hlsl
uint bodyCount;  // set from C#
if (id >= bodyCount) return;
```

---

### 7. GPU race condition on `majorBodies[i].collided` (NBodyVerletIntegration.compute, line ~98)

```hlsl
majorBodies[i].collided += 1.0;
```

Hundreds of threads may hit the same major body simultaneously. `+=` on a `RWStructuredBuffer` is not atomic — the increment will be lost for most collisions. Use `InterlockedAdd` on an integer counter, or track collisions in a separate buffer.

---

### 8. `BinaryFileLoader` — Hard-coded file paths (BinaryFileLoader.cs, lines ~28-30)

```csharp
var comets = BinaryDataReader.LoadData(@"C:\Users\Jon\Downloads\dastcom5\exe\output\bodies.dat");
```

Absolute paths to a specific user's Downloads folder. Will crash on any other machine. Should use `Application.streamingAssetsPath` or a configurable path.

---

### 9. `CreateSphere()` uses non-exclusive `if` chain (NBodySimulation.cs, lines ~140-210)

Every branch is a standalone `if` (not `else if`), so all 11 conditions are evaluated even after match. When `i == 0`, the remaining 10 checks still run. Should use `else if` or `switch`.

---

### 10. `NBodyShader.compute` (Barnes-Hut) — `Body` struct mismatch

The `Body` struct in `NBodyShader.compute` has **8 floats** (no `isComet` field):

```hlsl
struct Body { float3 position; float3 velocity; float mass; float collided; };
```

But the C# side creates buffers with stride = 9 floats (includes `isComet`). If this shader were re-enabled, the offset mismatch would corrupt all data after the `mass` field.

---

### 11. `CoroutineJsonLoader` — Velocities in wrong units

`ComputeStateVector` outputs velocity in **AU/day** (since `mu` is in AU³/day²), but the simulation expects m/s. The loaded comets will move ~172× too slowly (1 AU/day ≈ 1731 m/s). A unit conversion is needed:

```csharp
velocity *= 1.731e3f; // AU/day → m/s (approximate)
```

---

## Performance Optimizations

### P1. `FindKernel()` called every frame (NBodySimulation.cs, `Update()`)

```csharp
void Update()
{
    int kernel = computeShader.FindKernel("Simulate");  // string lookup every frame
```

`FindKernel` does a string lookup. Cache the kernel index once in `Initialize()`:

```csharp
private int _simulateKernel;
// in Initialize():
_simulateKernel = computeShader.FindKernel("Simulate");
```

---

### P2. GPU ↔ CPU round-trip every frame (`majorBodyBuffer.GetData`)

```csharp
majorBodyBuffer.SetData(MajorBodies);  // CPU → GPU
computeShader.Dispatch(...);
majorBodyBuffer.GetData(MajorBodies);  // GPU → CPU  ← pipeline stall
```

`GetData` forces a full GPU pipeline flush and synchronous readback. For hundreds of thousands of bodies, this stalls both the CPU and GPU. Options:

- **`AsyncGPUReadback.Request()`** — non-blocking readback, process collision counts one frame later.
- Only read back collision counts via a small separate buffer (11 ints) instead of the entire major body array.

---

### P3. `StepSimulation()` allocates arrays every frame (NBodySimulation.cs, line ~520)

```csharp
Vector3[] currentPositions = new Vector3[n];
Vector3[] a_t = new Vector3[n];
Vector3[] newPositions = new Vector3[n];
Vector3[] a_t_dt = new Vector3[n];
```

Four heap allocations per frame (n=11 each). Pre-allocate these as class fields to avoid GC pressure.

---

### P4. Compute shader: softening conversion repeated per interaction

```hlsl
float dist = length(d) + softening * AU_TO_METERS;
```

`softening * AU_TO_METERS` is constant — compute it once into a local:

```hlsl
float softeningMeters = softening * AU_TO_METERS;
// ...loop...
float dist = length(d) + softeningMeters;
```

Similarly, `major.position * AU_TO_METERS` is done twice (in the a0 loop and a1 loop) for the same major body. Caching all major positions in shared memory would halve global memory reads.

---

### P5. Use `groupshared` memory for major bodies (NBodyVerletIntegration.compute)

Each of the 256 threads in a group independently reads all 11 major bodies from global memory. Since `majorBodyCount` is small (11), load them into `groupshared` memory once per group:

```hlsl
groupshared Body sharedMajor[16]; // fits 11 easily
// thread 0 loads, then GroupMemoryBarrierWithGroupSync()
```

This eliminates redundant global reads (256 × 11 → 11).

---

### P6. `FixedLengthTrail` — `RemoveAt(0)` on a `List<>` is O(n)

```csharp
segmentLengths.RemoveAt(0);
positions.RemoveAt(0);
```

Removing from the front of a `List` shifts all elements. For a trail with hundreds of points, this is expensive every frame. Use a `LinkedList<>` or circular buffer instead.

---

### P7. `FixedLengthTrail` — `ToArray()` allocation every frame

```csharp
lineRenderer.SetPositions(positions.ToArray());
```

Allocates a new array every frame. Use `lineRenderer.SetPositions()` with a reusable `Vector3[]` buffer, or use `CollectionsMarshal.AsSpan()` with `.NET 5+`.

---

### P8. `ResetMass()` and `SetMass()` call `GetSolarSystemBodies()` (allocating a fresh array each time)

```csharp
var normalMassBodies = BinaryFileLoader.GetSolarSystemBodies();
```

This creates 11 `Body` structs just to read default masses. Cache the reference data once at startup.

---

### P9. `OnRenderObject()` renders all bodies including collided ones

The draw call issues vertices for the entire `Bodies.Length`. Collided bodies are discarded in the fragment shader via `clip()`, but their vertices are still processed. Over time, as more bodies collide, GPU work stays constant. Options:

- Compact the buffer periodically (remove collided bodies).
- Use indirect draw with `Graphics.DrawProceduralIndirectNow` and an `AppendStructuredBuffer` on the GPU to only emit live bodies.

---

### P10. `NBodyShader.shader` — `GetAsteroidGlow()` is defined but never called

The function computes a glow color but nothing invokes it. Either dead code to remove, or it should be added to `GetBodyColor()`:

```hlsl
float3 glow = GetAsteroidGlow(body.mass);
return lerp(baseColor, beltColor, 0.4) + glow;
```

---

### P11. `NBodyShader.shader` — Asteroids with low mass render as black

When `normalizedMass` ≈ 0, `brightnessFactor` = 0, and `baseColor *= 0` = black. Small asteroids become invisible (blended with additive black = nothing). Consider a minimum brightness floor:

```hlsl
float brightnessFactor = max(normalizedMass, 0.15);
```

---

### P12. `ComputeAcceleration()` — Softening applied incorrectly vs. GPU

CPU (NBodySimulation.cs):
```csharp
float distanceSqr = LengthSquared(direction) + softeningMeters * softeningMeters;
float invDistance = 1.0f / MathF.Sqrt(distanceSqr);
```

GPU (NBodyVerletIntegration.compute):
```hlsl
float dist = length(d) + softening * AU_TO_METERS;
```

The CPU uses **Plummer softening** ($\sqrt{r^2 + \epsilon^2}$) while the GPU uses **linear softening** ($r + \epsilon$). These give different force profiles, meaning the CPU major bodies and GPU small bodies experience subtly different gravitational physics. Pick one formulation for consistency — Plummer is standard:

```hlsl
float distSqr = dot(d, d) + softeningMeters * softeningMeters;
float dist = sqrt(distSqr);
```

---

## Summary

| Category | Count |
|----------|-------|
| **Bugs / Errors** | 11 |
| **Performance Optimizations** | 12 |

### Priority Items

| Priority | Issue | Impact |
|----------|-------|--------|
| **Critical** | #3 — Comet mass always 0 | All comets have no mass, may affect rendering and physics |
| **Critical** | #6 — GPU out-of-bounds access | Undefined behavior / potential crash |
| **Critical** | #7 — Race condition on collision counter | Collision counts are unreliable |
| **High** | #1 — Mercury `*=` bug | Mercury mass goes haywire on slider change |
| **High** | #2 — ToggleTrail logic error | Trail toggle broken for TrailRenderer path |
| **High** | #12/P12 — Softening inconsistency | CPU and GPU use different gravity formulas |
| **Medium** | P2 — Synchronous GPU readback | Main pipeline stall every frame |
| **Medium** | P5 — No shared memory for major bodies | Redundant global memory reads |
| **Low** | P1, P3, P6, P7, P8 | Minor per-frame allocations and lookups |
