using UnityEngine;
using TMPro;

/// <summary>
/// Tracks the number of bodies consumed by the black hole and updates a UI label.
/// Attach to a UI element with TextMeshProUGUI, or to the NBodySimulationManager.
/// </summary>
public class BlackHoleConsumptionCounter : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI counterText;

    private NBodySimulation simulation;
    private const int BLACK_HOLE_INDEX = 11;
    private int lastCount = 0;

    void Start()
    {
        var manager = GameObject.FindGameObjectWithTag("NBodySimulationManager");
        if (manager != null)
        {
            simulation = manager.GetComponent<NBodySimulation>();
        }
    }

    void Update()
    {
        if (simulation == null || simulation.MajorBodies == null
            || simulation.MajorBodies.Length <= BLACK_HOLE_INDEX)
            return;

        int consumedCount = (int)simulation.MajorBodies[BLACK_HOLE_INDEX].collided;
        if (consumedCount != lastCount)
        {
            lastCount = consumedCount;
            if (counterText != null)
            {
                counterText.text = $"Consumed: {consumedCount}";
            }
        }
    }
}
