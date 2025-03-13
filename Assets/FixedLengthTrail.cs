using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FixedLengthTrail : MonoBehaviour
{
  [Tooltip("Maximum length of the trail in world units.")]
  public float maxTrailLength = 10f;
  [Tooltip("Minimum distance between recorded points.")]
  public float minDistance = 0.1f;

  private LineRenderer lineRenderer;
  // List to store the trail points.
  private List<Vector3> positions = new List<Vector3>();
  // List to store distances between consecutive points.
  private List<float> segmentLengths = new List<float>();
  private float totalLength = 0f;

  void Start()
  {
    // Set up the Line Renderer.
    lineRenderer = gameObject.GetComponent<LineRenderer>();

    // Start with the current position.
    Vector3 startPos = transform.position;
    positions.Add(startPos);
  }

  void Update()
  {
    Vector3 currentPos = transform.position;

    // Add a new point if the object has moved far enough.
    if (Vector3.Distance(positions[positions.Count - 1], currentPos) >= minDistance)
    {
      // Calculate the distance from the last recorded position.
      float d = Vector3.Distance(positions[positions.Count - 1], currentPos);
      segmentLengths.Add(d);
      totalLength += d;
      positions.Add(currentPos);
    }

    // Remove or trim points from the start if the trail is too long.
    while (totalLength > maxTrailLength && positions.Count > 1)
    {
      float firstSegment = segmentLengths[0];
      // If removing the entire first segment would exceed the limit...
      if (totalLength - firstSegment < maxTrailLength)
      {
        // Calculate how much of the segment to remove.
        float excess = totalLength - maxTrailLength;
        // Interpolate between the first two points to get the new starting point.
        Vector3 newFirstPos = Vector3.Lerp(positions[0], positions[1], excess / firstSegment);
        positions[0] = newFirstPos;
        totalLength = maxTrailLength;
        break;
      }
      else
      {
        // Remove the whole segment.
        totalLength -= firstSegment;
        segmentLengths.RemoveAt(0);
        positions.RemoveAt(0);
      }
    }

    // Update the Line Renderer with the current set of positions.
    lineRenderer.positionCount = positions.Count;
    lineRenderer.SetPositions(positions.ToArray());
  }
}
