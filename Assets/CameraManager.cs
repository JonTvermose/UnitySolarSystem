using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
  public GameObject targetObject;

  /// <summary>
  /// The current distance from the camera to the target.
  /// </summary>
  public float distance = 50.0f;

  /// <summary>
  /// Speed factors for mouse rotation.
  /// </summary>
  public float xSpeed = 500.0f;
  public float ySpeed = 500.0f;

  /// <summary>
  /// Limits for the vertical rotation angle (in degrees).
  /// </summary>
  public float yMinLimit = 10f;
  public float yMaxLimit = 80f;

  /// <summary>
  /// Limits for how far in or out you can zoom.
  /// </summary>
  public float distanceMin = 10f;
  public float distanceMax = 100f;

  // Internal variables to store current rotation angles.
  private float x = 0.0f;
  private float y = 0.0f;

  void Start()
  {
    // Initialize the rotation angles based on the current transform.
    Vector3 angles = transform.eulerAngles;
    x = angles.y;
    y = angles.x;
  }

  void LateUpdate()
  {
    // Rotate camera when left mouse button is held down.
    if (Input.GetMouseButton(0))
    {
      // Mouse X controls horizontal rotation (around the Y axis).
      x += Input.GetAxis("Mouse X") * xSpeed * Time.deltaTime;
      // Mouse Y controls vertical rotation (around the X axis).
      y -= Input.GetAxis("Mouse Y") * ySpeed * Time.deltaTime;
      y = ClampAngle(y, yMinLimit, yMaxLimit);
    }

    // Optional: Rotate with arrow keys if desired.
    float horizontal = Input.GetAxis("Horizontal");
    float vertical = Input.GetAxis("Vertical");
    if (Mathf.Abs(horizontal) > 0.01f)
    {
      x += horizontal * xSpeed * Time.deltaTime;
    }
    if (Mathf.Abs(vertical) > 0.01f)
    {
      y -= vertical * ySpeed * Time.deltaTime;
      y = ClampAngle(y, yMinLimit, yMaxLimit);
    }

    // Zoom in/out with the mouse scroll wheel.
    float scroll = Input.GetAxis("Mouse ScrollWheel");
    if (Mathf.Abs(scroll) > 0.001f)
    {
      distance = Mathf.Clamp(distance - scroll * distance, distanceMin, distanceMax);
    }

    // Compute the new rotation and position of the camera.
    Quaternion rotation = Quaternion.Euler(y, x, 0);
    Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
    Vector3 position = rotation * negDistance + targetObject.transform.position;

    transform.rotation = rotation;
    transform.position = position;
  }

  /// <summary>
  /// Clamps an angle between a minimum and maximum value.
  /// </summary>
  private float ClampAngle(float angle, float min, float max)
  {
    if (angle < -360f) angle += 360f;
    if (angle > 360f) angle -= 360f;
    return Mathf.Clamp(angle, min, max);
  }
}
