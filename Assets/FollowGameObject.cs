using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FollowGameObject : MonoBehaviour
{
  public Transform target; // The GameObject to follow
  public Camera mainCamera;
  public Vector3 screenOffset = new Vector3(0, 0, 0); // Offset in screen space (pixels)
  public float baseScale = 0.1f; // Base scale multiplier
  public float minDistance = 0.5f; // Minimum distance from the object to prevent clipping
  private TextMeshProUGUI textMeshPro;
  public float maxShownDistance = 70;

  void Start()
  {
    if (mainCamera == null)
    {
      mainCamera = Camera.main; // Get main camera if not assigned
    }

    textMeshPro = GetComponent<TextMeshProUGUI>();

  }

  void LateUpdate()
  {
    if (target == null || mainCamera == null) return;


    // Convert world position to screen position
    Vector3 screenPos = mainCamera.WorldToScreenPoint(target.position);

    // Add screen-space offset
    screenPos += new Vector3(screenOffset.x, screenOffset.y, 0);

    // Convert back to world space
    Vector3 worldOffsetPos = mainCamera.ScreenToWorldPoint(screenPos);

    // Ensure the text is always in front of the target
    Vector3 toCamera = (mainCamera.transform.position - target.position).normalized;
    float distanceToCamera = Vector3.Distance(mainCamera.transform.position, target.position);
    float offsetDistance = Mathf.Max(distanceToCamera * 0.05f, minDistance); // Adjust offset dynamically

    // Move the text in front of the object
    worldOffsetPos += toCamera * offsetDistance;

    // Set text position
    transform.position = worldOffsetPos;

    // **Ensure the text always faces the camera AND remains horizontal in screen space**
    transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position, mainCamera.transform.up);

    // Maintain a constant size
    float distance = Vector3.Distance(transform.position, mainCamera.transform.position);
    transform.localScale = Vector3.one * distance * baseScale;

    if(distance > maxShownDistance)
    {
      textMeshPro.enabled = false;
    }
    else
    {
      textMeshPro.enabled = true;
    }
  }
}
