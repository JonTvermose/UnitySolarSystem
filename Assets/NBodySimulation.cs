// NBodyManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class NBodySimulation : MonoBehaviour
{
  // Must match the Body struct in the compute shader.
  public struct Body
  {
    public Vector3 position;
    public Vector3 velocity;
    public float mass;
    public float isComet;
    public float collided;
  }

  // A flattened tree node structure (again, must match the compute shader).
  // In a complete Barnes�Hut algorithm you would have many nodes.
  struct Node
  {
    public Vector3 center;      // center-of-mass of the node
    public float mass;          // total mass
    public Vector3 minBounds;   // bounding box of the node
    public Vector3 maxBounds;
    public int childStart;      // index of the first child in the node array
    public int childCount;      // number of children (0 if leaf)
    public int bodyIndex;       // if leaf, the index of the body (or -1)
  }

  [Header("Simulation Settings")]
  public float timeStep = 0.01f;
  public float G = 6.67430e-11f;
  public float theta = 0.5f;
  public float softening = 0.1f;
  public float collisionScale = 1.0f;

  [Header("UI References")]
  public Slider speedSlider;
  public Button toggleTrail;
  public Button resetMass;
  public Button resetSpeed;

  [Header("References")]
  public ComputeShader computeShader;
  public Material material;

  [Header("Solar system")]
  public GameObject sun;
  public GameObject mercury;
  public GameObject venus;
  public GameObject earth;
  public GameObject mars;
  public GameObject jupiter;
  public GameObject saturn;
  public GameObject uranus;
  public GameObject neptune;
  public GameObject pluto;
  public GameObject ceres;
  public GameObject blackHole;
  public GameObject moon;

  ComputeBuffer bodyBuffer;
  ComputeBuffer majorBodyBuffer;
  ComputeBuffer nodeBuffer;
  public Body[] Bodies;
  public Body[] MajorBodies;

  // In a full implementation, you�d rebuild the tree each frame.
  // For this simple example, we only build a single-node tree (the root).
  Node[] nodes;

  private List<GameObject> _majorBodies = new();

  private bool _initialized = false;

  // Each Body has 9 floats (3 + 3 + 1 + 1 + 1).
  private int bodyStride = sizeof(float) * 9;

  public void Initialize()
  {
    _initialized = false;

    ToggleTrail(forceDisable: true);
    ResetSpeed();
    ResetMass();
    foreach(var obj in _majorBodies)
    {
      Destroy(obj);
    }
    _majorBodies = new();
    for (int i = 0; i < MajorBodies.Length; i++)
    {
      var obj = CreateSphere(MajorBodies[i].position, 0.05f, i, "Body " + i);
      _majorBodies.Add(obj);
    }
    Camera.main.GetComponent<CameraManager>().targetObject = _majorBodies.First();

    // Create compute buffers for the bodies and set data
    if(bodyBuffer == null)
    {
      bodyBuffer = new ComputeBuffer(Bodies.Length, bodyStride);
    }
    bodyBuffer.SetData(Bodies);
    if(majorBodyBuffer == null)
    {
      majorBodyBuffer = new ComputeBuffer(MajorBodies.Length, bodyStride);
    }
    majorBodyBuffer.SetData(MajorBodies);

    // Pass the buffer to both the compute shader and the material.
    int kernel = computeShader.FindKernel("Simulate");
    computeShader.SetBuffer(kernel, "bodies", bodyBuffer);
    computeShader.SetBuffer(kernel, "majorBodies", majorBodyBuffer);
    material.SetBuffer("bodies", bodyBuffer);
    material.SetBuffer("majorBodies", majorBodyBuffer);
    material.SetInt("_Mode", 1);

    // Set constant simulation parameters.
    computeShader.SetFloat("deltaTime", timeStep);
    computeShader.SetFloat("G", G);
    computeShader.SetFloat("theta", theta);
    computeShader.SetFloat("softening", softening);
    computeShader.SetFloat("collisionScale", collisionScale);
    computeShader.SetInt("majorBodyCount", MajorBodies.Length);

    //RebuildBarnesHutTree();
    _initialized = true;
  }

  public GameObject CreateSphere(Vector3 position, float radius, int i, string name = "Sphere")
  {
    GameObject sphere = null;
    if (i == 0)
    {
      sphere = GameObject.Instantiate(sun, position, Quaternion.identity);
      sphere.name = "Sun";
    }
    if (i == 1)
    {
      sphere = GameObject.Instantiate(mercury, position, Quaternion.identity);
      sphere.name = "Mercury";
      radius = 0.02f;
    }
    if (i == 2)
    {
      sphere = GameObject.Instantiate(venus, position, Quaternion.identity);
      sphere.name = "Venus";
      radius = 0.0475f;
    }
    if (i == 3)
    {
      sphere = GameObject.Instantiate(earth, position, Quaternion.identity);
      sphere.name = "Earth";
      radius = 0.05f;
    }
    if (i == 4)
    {
      sphere = GameObject.Instantiate(mars, position, Quaternion.identity);
      sphere.name = "Mars";
      radius = 0.0266f;
    }
    if (i == 5)
    {
      sphere = GameObject.Instantiate(jupiter, position, Quaternion.identity);
      sphere.name = "Jupiter";
      radius = 0.07f;
    }
    if (i == 6)
    {
      sphere = GameObject.Instantiate(saturn, position, Quaternion.identity);
      sphere.name = "Saturn";
      radius = 0.07f;
    }
    if (i == 7)
    {
      sphere = GameObject.Instantiate(uranus, position, Quaternion.identity);
      sphere.name = "Uranus";
      radius = 0.07f;
    }
    if (i == 8)
    {
      sphere = GameObject.Instantiate(neptune, position, Quaternion.identity);
      sphere.name = "Neptune";
      radius = 0.07f;
    }
    if (i == 9)
    {
      sphere = GameObject.Instantiate(pluto, position, Quaternion.identity);
      sphere.name = "Pluto";
      radius = 0.02f;
    }
    if (i == 10)
    {
      sphere = GameObject.Instantiate(ceres, position, Quaternion.identity);
      sphere.name = "Ceres";
      radius = 0.02f;
    }
    if (i == 11)
    {
      sphere = GameObject.Instantiate(blackHole, position, Quaternion.identity);
      sphere.name = "BlackHole";
      radius = 0.04f;
    }
    if (sphere == null)
    {
      sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      // Create and assign a new material
      Material newMaterial = new Material(Shader.Find("Standard"));
      newMaterial.color = Color.red; // Set color to red
      sphere.GetComponent<Renderer>().material = newMaterial;
      sphere.name = name;
    }
    sphere.transform.position = position;
    sphere.transform.localScale = i == 0 ? Vector3.one * 0.2f : Vector3.one * radius; // Adjust scale to match radius

    return sphere;
  }

  void Start()
  {
    //Adds a listener to the main slider and invokes a method when the value changes.
    speedSlider.onValueChanged.AddListener(delegate { ValueChangeCheck(); });
    toggleTrail.onClick.AddListener(delegate { ToggleTrail(null); });
    resetSpeed.onClick.AddListener(delegate { ResetSpeed(); });
    resetMass.onClick.AddListener(delegate { ResetMass(); });
  }

  public void SetMass(float massMultiplier, string targetName)
  {
    var normalMassBodies = BinaryFileLoader.GetSolarSystemBodies();
    switch (targetName)
    {
      case "Sun":
        MajorBodies[0].mass = normalMassBodies[0].mass * massMultiplier;
        break;
      case "Mercury":
        MajorBodies[1].mass *= normalMassBodies[1].mass * massMultiplier;
        break;
      case "Venus":
        MajorBodies[2].mass = normalMassBodies[2].mass * massMultiplier;
        break;
      case "Earth":
        MajorBodies[3].mass = normalMassBodies[3].mass * massMultiplier;
        break;
      case "Mars":
        MajorBodies[4].mass = normalMassBodies[4].mass * massMultiplier;
        break;
      case "Jupiter":
        MajorBodies[5].mass = normalMassBodies[5].mass * massMultiplier;
        break;
      case "Saturn":
        MajorBodies[6].mass = normalMassBodies[6].mass * massMultiplier;
        break;
      case "Uranus":
        MajorBodies[7].mass = normalMassBodies[7].mass * massMultiplier;
        break;
      case "Neptune":
        MajorBodies[8].mass = normalMassBodies[8].mass * massMultiplier;
        break;
      case "Pluto":
        MajorBodies[9].mass = normalMassBodies[9].mass * massMultiplier;
        break;
      case "Ceres":
        MajorBodies[10].mass = normalMassBodies[10].mass * massMultiplier; 
        break;
      case "BlackHole":
        // Clamp mass multiplier to prevent extreme values from destabilizing the sim
        massMultiplier = Mathf.Clamp(massMultiplier, 0f, 10f);
        MajorBodies[11].mass = normalMassBodies[11].mass * massMultiplier;
        break;
      default:
        Debug.LogError("Unknown target name: " + targetName);
        break;
    }
  }

  public void ResetMass()
  {
    var normalMassBodies = BinaryFileLoader.GetSolarSystemBodies();
    for (int i = 0; i < MajorBodies.Length; i++)
    {
      MajorBodies[i].mass = normalMassBodies[i].mass;
    }
    var massSliders = GameObject.FindGameObjectsWithTag("MassSlider");
    for (int i = 0; i < massSliders.Length; i++)
    {
      massSliders[i].GetComponent<Slider>().value = 10.0f;
    }
  }

  public void ResetSpeed()
  {
    speedSlider.value = 5000.0f;
    timeStep = 5000.0f;
    computeShader.SetFloat("deltaTime", timeStep);
  }

  public void ToggleTrail(bool? forceDisable)
  {
    var objs = GameObject.FindGameObjectsWithTag("PlanetTrail").ToList();
    foreach (var o in objs)
    {
      var trail = o.GetComponent<TrailRenderer>();
      if(trail == null)
      {
        var particleTrail = o.GetComponent<ParticleSystem>();
        if(particleTrail.isPlaying && particleTrail.isEmitting)
        {
          particleTrail.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        else if(forceDisable == null || !forceDisable.Value)
        {
          particleTrail.Play(true);
        }
      }
      else
      {
        trail.time = trail.time == 0 && !(forceDisable == null && forceDisable.Value) ? 15 : 0;
      }
    }
  }

  public void ValueChangeCheck()
  {
    timeStep = speedSlider.value;
    computeShader.SetFloat("deltaTime", timeStep);
  }

  void Update()
  {
    if (!_initialized)
      return;

    //RebuildBarnesHutTree();

    int kernel = computeShader.FindKernel("Simulate");
    majorBodyBuffer.SetData(MajorBodies);
    computeShader.SetBuffer(kernel, "majorBodies", majorBodyBuffer);

    // Dispatch the compute shader.
    // Use enough thread groups so that numBodies threads are executed.
    computeShader.Dispatch(kernel, Mathf.CeilToInt(Bodies.Length / 256.0f), 1, 1);
    //DebugBufferContents();

    majorBodyBuffer.GetData(MajorBodies);
    StepSimulation(timeStep);

    // Update the major body buffer on the rendering material so shaders
    // can read live BH position for color-shift / spaghettification effects
    material.SetBuffer("majorBodies", majorBodyBuffer);
  }

  void OnRenderObject()
  {
    if (!_initialized)
      return;

    // Render the bodies using the material.
    material.SetPass(0);
    //// Draw a point for each body.
    Graphics.DrawProceduralNow(MeshTopology.Points, Bodies.Length);
  }


  void OnDestroy()
  {
    // Release compute buffers.
    if (bodyBuffer != null)
      bodyBuffer.Release();
    if (majorBodyBuffer != null)
      majorBodyBuffer.Release();
    if (nodeBuffer != null)
      nodeBuffer.Release();
  }

  // Stride for Node buffer (calculate based on the fields: 3+1+3+3 floats and 3 ints).
  int nodeStride = sizeof(float) * (3 + 1 + 3 + 3) + sizeof(int) * 3;


  /// <summary>
  /// Rebuilds the Barnes�Hut tree by recursively subdividing the domain.
  /// </summary>
  public void RebuildBarnesHutTree()
  {
    if (MajorBodies == null || MajorBodies.Length == 0)
      return;

    // Determine the global bounding box from all bodies.
    Vector3 minBounds = MajorBodies[0].position;
    Vector3 maxBounds = MajorBodies[0].position;
    for (int i = 1; i < MajorBodies.Length; i++)
    {
      minBounds = Vector3.Min(minBounds, MajorBodies[i].position);
      maxBounds = Vector3.Max(maxBounds, MajorBodies[i].position);
    }

    // Create a list of all body indices.
    List<int> allIndices = new List<int>();
    for (int i = 0; i < MajorBodies.Length; i++)
      allIndices.Add(i);

    // Build the tree recursively.
    List<Node> nodeList = new List<Node>();
    BuildTree(allIndices, minBounds, maxBounds, nodeList, 0);

    // Convert the list to an array and update the node buffer.
    nodes = nodeList.ToArray();
    if (nodeBuffer != null)
      nodeBuffer.Release();
    nodeBuffer = new ComputeBuffer(nodes.Length, nodeStride);
    nodeBuffer.SetData(nodes);

    // Bind the updated node buffer to the compute shader.
    int kernel = computeShader.FindKernel("Simulate");
    computeShader.SetBuffer(kernel, "majorNodes", nodeBuffer);

  }

  int maxDepth = 200; // Maximum allowed recursion depth.

  int BuildTree(List<int> indices, Vector3 min, Vector3 max, List<Node> nodeList, int depth)
  {
    // If maximum recursion depth is reached, treat the node as a leaf.
    if (depth > maxDepth)
    {
      Node leaf = new Node();
      float totalMass = 0f;
      Vector3 weightedPos = Vector3.zero;
      foreach (int idx in indices)
      {
        totalMass += MajorBodies[idx].mass;
        weightedPos += MajorBodies[idx].position * MajorBodies[idx].mass;
      }
      leaf.mass = totalMass;
      leaf.center = totalMass > 0 ? weightedPos / totalMass : (min + max) * 0.5f;
      leaf.minBounds = min;
      leaf.maxBounds = max;
      leaf.childCount = 0;
      leaf.childStart = -1;
      // You can mark leaf.bodyIndex as -1 to indicate it is an aggregate leaf.
      leaf.bodyIndex = -1;

      int myIndex = nodeList.Count;
      nodeList.Add(leaf);
      return myIndex;
    }

    // Compute total mass, center-of-mass, and bounding box for the current set.
    Node node = new Node();
    float totalMassCurrent = 0f;
    Vector3 weightedPosCurrent = Vector3.zero;
    foreach (int idx in indices)
    {
      totalMassCurrent += MajorBodies[idx].mass;
      weightedPosCurrent += MajorBodies[idx].position * MajorBodies[idx].mass;
    }
    Vector3 com = totalMassCurrent > 0 ? weightedPosCurrent / totalMassCurrent : (min + max) * 0.5f;

    node.mass = totalMassCurrent;
    node.center = com;
    node.minBounds = min;
    node.maxBounds = max;

    // If only one body is present, create a leaf.
    if (indices.Count == 1)
    {
      node.bodyIndex = indices[0];
      node.childCount = 0;
      node.childStart = -1;
      int myIndex = nodeList.Count;
      nodeList.Add(node);
      return myIndex;
    }
    else
    {
      node.bodyIndex = -1; // Not a single body.
      int myIndex = nodeList.Count;
      nodeList.Add(node);  // Reserve a spot for the parent.

      // Subdivide the region into 8 octants.
      Vector3 mid = (min + max) * 0.5f;
      List<int>[] octantIndices = new List<int>[8];
      for (int i = 0; i < 8; i++)
      {
        octantIndices[i] = new List<int>();
      }

      // Partition the indices by octant.
      foreach (int idx in indices)
      {
        Vector3 pos = MajorBodies[idx].position;
        int oct = 0;
        if (pos.x >= mid.x) oct |= 1;
        if (pos.y >= mid.y) oct |= 2;
        if (pos.z >= mid.z) oct |= 4;
        octantIndices[oct].Add(idx);
      }

      // Recursively build children for non-empty octants.
      int childStart = nodeList.Count; // Children will be added immediately after this node.
      int childCount = 0;
      for (int oct = 0; oct < 8; oct++)
      {
        if (octantIndices[oct].Count > 0)
        {
          Vector3 octMin = new Vector3(
              (oct & 1) == 1 ? mid.x : min.x,
              (oct & 2) == 2 ? mid.y : min.y,
              (oct & 4) == 4 ? mid.z : min.z);
          Vector3 octMax = new Vector3(
              (oct & 1) == 1 ? max.x : mid.x,
              (oct & 2) == 2 ? max.y : mid.y,
              (oct & 4) == 4 ? max.z : mid.z);

          BuildTree(octantIndices[oct], octMin, octMax, nodeList, depth + 1);
          childCount++;
        }
      }

      // Update parent node with children information.
      Node updatedParent = nodeList[myIndex];
      updatedParent.childStart = childStart;
      updatedParent.childCount = childCount;
      nodeList[myIndex] = updatedParent;

      return myIndex;
    }
  }

  // Conversion factor: 1 AU = 1.496e11 meters
  private const float AU_TO_METERS = 1.496e11f;

  /// <summary>
  /// Performs one simulation step using the velocity Verlet integration method.
  /// </summary>
  /// <param name="dt">Time step in seconds.</param>
  public void StepSimulation(float dt)
  {
    int n = MajorBodies.Length;
    // Save the current positions (in AU).
    Vector3[] currentPositions = new Vector3[n];
    for (int i = 0; i < n; i++)
    {
      currentPositions[i] = MajorBodies[i].position;
    }

    // 1. Compute accelerations at time t (in m/s^2)
    Vector3[] a_t = new Vector3[n];
    for (int i = 0; i < n; i++)
    {
      a_t[i] = ComputeAcceleration(currentPositions, i);
    }

    // 2. Update positions.
    //    The displacement is computed in meters:
    //    displacement = v * dt + 0.5 * a * dt^2   [m]
    //    We then convert the displacement from meters to AU.
    Vector3[] newPositions = new Vector3[n];
    for (int i = 0; i < n; i++)
    {
      Vector3 displacementInMeters = MajorBodies[i].velocity * dt + 0.5f * a_t[i] * dt * dt;
      newPositions[i] = MajorBodies[i].position + displacementInMeters / AU_TO_METERS;
    }

    // 3. Compute accelerations at time t+dt using the new positions.
    Vector3[] a_t_dt = new Vector3[n];
    for (int i = 0; i < n; i++)
    {
      a_t_dt[i] = ComputeAcceleration(newPositions, i);
    }

    // 4. Update velocities:
    //    v(t+dt) = v(t) + 0.5*(a(t) + a(t+dt))*dt
    //    (Here the accelerations are in m/s^2 and dt in seconds so the units match.)
    for (int i = 0; i < n; i++)
    {
      MajorBodies[i].velocity += 0.5f * (a_t[i] + a_t_dt[i]) * dt;
      MajorBodies[i].position = newPositions[i];
      _majorBodies[i].transform.position = MajorBodies[i].position;
    }
  }

  /// <summary>
  /// Computes the gravitational acceleration on body i using the positions (in AU).
  /// The returned acceleration is in m/s^2.
  /// </summary>
  /// <param name="positions">Positions of all bodies (in AU).</param>
  /// <param name="i">Index of the body for which to compute acceleration.</param>
  /// <returns>Acceleration vector in m/s^2.</returns>
  private Vector3 ComputeAcceleration(Vector3[] positions, int i)
  {
    // Convert the position of body i from AU to meters.
    Vector3 pos_i_m = positions[i] * AU_TO_METERS;
    Vector3 acceleration = Vector3.zero;

    for (int j = 0; j < MajorBodies.Length; j++)
    {
      if (i == j)
        continue;
      // Convert position of body j from AU to meters.
      Vector3 pos_j_m = positions[j] * AU_TO_METERS;
      Vector3 direction = pos_j_m - pos_i_m;

      // Convert the softening parameter from AU to meters.
      float softeningMeters = softening * AU_TO_METERS;
      float distanceSqr = LengthSquared(direction) + softeningMeters * softeningMeters;
      float invDistance = 1.0f / MathF.Sqrt(distanceSqr);
      float invDistanceCube = invDistance * invDistance * invDistance;

      acceleration += direction * (G * MajorBodies[j].mass * invDistanceCube);
    }
    return acceleration;
  }

  private float LengthSquared(Vector3 v)
  {
    return v.x * v.x + v.y * v.y + v.z * v.z;
  }
}
