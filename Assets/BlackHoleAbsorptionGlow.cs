using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Produces a brief screen flash whenever new bodies are consumed by the black hole.
/// Attach to a full-screen UI Image (initially transparent).
/// </summary>
public class BlackHoleAbsorptionGlow : MonoBehaviour
{
    [Header("Flash Settings")]
    public Color flashColor = new Color(1f, 0.6f, 0.1f, 0.4f);
    public float flashDuration = 0.15f;
    public float flashIntensityPerBody = 0.1f;

    private Image flashImage;
    private NBodySimulation simulation;
    private const int BLACK_HOLE_INDEX = 11;
    private float lastCollisionCount = 0f;
    private float currentAlpha = 0f;

    void Start()
    {
        flashImage = GetComponent<Image>();
        if (flashImage != null)
        {
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        }

        var manager = GameObject.FindGameObjectWithTag("NBodySimulationManager");
        if (manager != null)
        {
            simulation = manager.GetComponent<NBodySimulation>();
        }
    }

    void Update()
    {
        if (simulation == null || simulation.MajorBodies == null
            || simulation.MajorBodies.Length <= BLACK_HOLE_INDEX || flashImage == null)
            return;

        float currentCount = simulation.MajorBodies[BLACK_HOLE_INDEX].collided;
        float newHits = currentCount - lastCollisionCount;

        if (newHits > 0)
        {
            // Flash intensity proportional to how many bodies were consumed this frame
            currentAlpha = Mathf.Min(currentAlpha + newHits * flashIntensityPerBody, flashColor.a);
        }

        lastCollisionCount = currentCount;

        // Fade out
        currentAlpha = Mathf.Max(0f, currentAlpha - Time.deltaTime / flashDuration);
        flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, currentAlpha);
    }
}
