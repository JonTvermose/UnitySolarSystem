using UnityEngine;

/// <summary>
/// Image effect that performs gravitational lensing + accretion disc overlay
/// driven by the live N-body simulation.  Attach to the main camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class BlackHoleEffect : MonoBehaviour
{
  [Header("Shader Materials")]
  public Material lensMaterial;   // uses BlackHoleLensing.shader
  public Material diskMaterial;   // uses AccretionDisk.shader

  [Header("Black Hole Properties")]
  public Vector3 blackHoleWorldPos = Vector3.zero;
  public float blackHoleMass = 1f;
  public float blackHoleRadius = 0.5f;
  public float lensingStrength = 1.5f;

  [Header("Accretion Disk Settings")]
  public float diskInnerRadius = 0.5f;
  public float diskOuterRadius = 2f;
  public Color diskColor = new Color(1f, 0.5f, 0.1f, 1f);
  public float diskEmission = 1.5f;
  public float diskSpinSpeed = 30f;

  [Header("Runtime")]
  public bool effectEnabled = true;

  [Header("Debug")]
  public bool debugDrawSphere = true;

  // Cached references
  private Camera cam;
  private NBodySimulation simulation;
  private const int BLACK_HOLE_INDEX = 11;
  private const float G = 6.67430e-11f;
  private const float C = 299792458f;

  private void Awake()
  {
    cam = GetComponent<Camera>();
    cam.allowHDR = true;
  }

  private void Start()
  {
    var manager = GameObject.FindGameObjectWithTag("NBodySimulationManager");
    if (manager != null)
    {
      simulation = manager.GetComponent<NBodySimulation>();
    }
  }

  private void LateUpdate()
  {
    if (simulation == null || simulation.MajorBodies == null
        || simulation.MajorBodies.Length <= BLACK_HOLE_INDEX)
    {
      effectEnabled = false;
      return;
    }

    effectEnabled = true;

    // Pull live data from the simulation
    var bh = simulation.MajorBodies[BLACK_HOLE_INDEX];
    blackHoleWorldPos = bh.position;
    blackHoleMass = bh.mass;

    // Schwarzschild radius in meters, converted to AU for display
    float rs_m = (2f * G * bh.mass) / (C * C);
    float rs_AU = rs_m / 1.496e11f;
    // Scale up so it is actually visible
    blackHoleRadius = Mathf.Max(rs_AU * 1000f, 0.01f);
  }

  private void OnRenderImage(RenderTexture src, RenderTexture dst)
  {
    if (!effectEnabled || lensMaterial == null || diskMaterial == null)
    {
      Graphics.Blit(src, dst);
      return;
    }

    // Quick frustum check: if BH is behind the camera, skip the effect
    Vector3 toHole = blackHoleWorldPos - cam.transform.position;
    if (Vector3.Dot(toHole, cam.transform.forward) < 0)
    {
      Graphics.Blit(src, dst);
      return;
    }

    // Project BH world position to screen UV [0,1]
    Vector3 screenPos = cam.WorldToViewportPoint(blackHoleWorldPos);
    // If off-screen by a large margin, skip
    if (screenPos.x < -0.5f || screenPos.x > 1.5f || screenPos.y < -0.5f || screenPos.y > 1.5f)
    {
      Graphics.Blit(src, dst);
      return;
    }
    Vector4 bhScreenPos = new Vector4(screenPos.x, screenPos.y, 0, 0);

    // Normalize mass to a 0-1+ range for shader consumption (1 = solar mass)
    float normalizedMass = blackHoleMass / 1.989e30f;

    // Screen-space radius for the disk (approximate: world radius / distance)
    float distToCam = Vector3.Distance(blackHoleWorldPos, cam.transform.position);
    float screenRadius = Mathf.Clamp(diskOuterRadius / Mathf.Max(distToCam, 0.01f) * 0.15f, 0.005f, 0.4f);

    // ---------- 1. Lensing pass ----------
    lensMaterial.SetVector("_BHScreenPos", bhScreenPos);
    lensMaterial.SetFloat("_Mass", normalizedMass);
    lensMaterial.SetFloat("_Radius", blackHoleRadius);
    lensMaterial.SetFloat("_LensingStrength", lensingStrength);

    RenderTexture temp = RenderTexture.GetTemporary(src.width, src.height, 0, src.format);
    Graphics.Blit(src, temp, lensMaterial);

    // ---------- 2. Accretion disc overlay ----------
    diskMaterial.SetVector("_BHScreenPos", bhScreenPos);
    diskMaterial.SetFloat("_ScreenRadius", screenRadius);
    diskMaterial.SetFloat("_InnerRadius", diskInnerRadius);
    diskMaterial.SetFloat("_OuterRadius", diskOuterRadius);
    diskMaterial.SetColor("_BaseColor", diskColor);
    diskMaterial.SetFloat("_Emission", diskEmission);
    diskMaterial.SetFloat("_SpinAngle", Mathf.Repeat(Time.time * diskSpinSpeed, 360f));

    Graphics.Blit(temp, dst, diskMaterial);

    RenderTexture.ReleaseTemporary(temp);
  }

  private void OnDrawGizmos()
  {
    if (!debugDrawSphere) return;
    Gizmos.color = Color.black;
    Gizmos.DrawSphere(blackHoleWorldPos, blackHoleRadius);
  }
}
