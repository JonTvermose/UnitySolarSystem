using UnityEngine;

/// <summary>
/// Generates a procedural accretion disk around the black hole using a particle system.
/// Attach this to the black hole prefab root. It creates a ring of orbiting hot particles
/// (orange/white) visible from all angles.
/// </summary>
public class AccretionDiskParticles : MonoBehaviour
{
    [Header("Disk Settings")]
    public float innerRadius = 0.02f;
    public float outerRadius = 0.06f;
    public int particleCount = 200;
    public float orbitSpeed = 60f; // degrees per second base speed

    [Header("Colors")]
    public Color innerColor = new Color(1f, 0.95f, 0.8f, 1f);  // white-hot
    public Color outerColor = new Color(1f, 0.4f, 0.05f, 0.6f); // orange

    private ParticleSystem ps;
    private NBodySimulation _simulation;

    void Start()
    {
        var manager = GameObject.FindGameObjectWithTag("NBodySimulationManager");
        if (manager != null)
        {
            _simulation = manager.GetComponent<NBodySimulation>();
        }
        SetupParticleSystem();
    }

    void SetupParticleSystem()
    {
        ps = gameObject.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = particleCount;
        main.startLifetime = Mathf.Infinity;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;
        main.loop = false;
        main.startSize = 0.003f;

        // Disable the default emission — we'll emit all at once
        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        // Renderer setup
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.SetColor("_Color", innerColor);

        // Emit particles in a ring
        EmitRing();
    }

    void EmitRing()
    {
        var emitParams = new ParticleSystem.EmitParams();
        for (int i = 0; i < particleCount; i++)
        {
            float t = (float)i / particleCount;
            float angle = t * 360f * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(innerRadius, outerRadius, Random.Range(0f, 1f));

            // Random slight tilt to give the disk some thickness
            float yOffset = Random.Range(-0.002f, 0.002f);

            Vector3 pos = new Vector3(
                Mathf.Cos(angle) * radius,
                yOffset,
                Mathf.Sin(angle) * radius
            );

            emitParams.position = pos;
            emitParams.velocity = Vector3.zero;
            emitParams.startLifetime = float.MaxValue;

            // Color: inner particles are white-hot, outer are orange
            float normalizedDist = (radius - innerRadius) / (outerRadius - innerRadius);
            emitParams.startColor = Color.Lerp(innerColor, outerColor, normalizedDist);
            emitParams.startSize = Mathf.Lerp(0.004f, 0.002f, normalizedDist);

            ps.Emit(emitParams, 1);
        }
    }

    void Update()
    {
        if (ps == null) return;

        float speedMultiplier = 1f;
        if (_simulation != null)
        {
            speedMultiplier = _simulation.timeStep / 100f;
        }

        // Rotate particles around the local Y axis to simulate orbital motion
        var particles = new ParticleSystem.Particle[ps.particleCount];
        int count = ps.GetParticles(particles);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = particles[i].position;
            float dist = new Vector2(pos.x, pos.z).magnitude;

            // Inner particles orbit faster (Keplerian-ish: speed ∝ 1/√r)
            float speed = orbitSpeed * speedMultiplier / Mathf.Max(Mathf.Sqrt(dist / innerRadius), 0.5f);
            float angleStep = speed * Time.deltaTime * Mathf.Deg2Rad;

            float cosA = Mathf.Cos(angleStep);
            float sinA = Mathf.Sin(angleStep);
            float newX = pos.x * cosA - pos.z * sinA;
            float newZ = pos.x * sinA + pos.z * cosA;

            particles[i].position = new Vector3(newX, pos.y, newZ);
        }

        ps.SetParticles(particles, count);
    }
}
