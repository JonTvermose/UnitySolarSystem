using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BodyRotation : MonoBehaviour
{

  public float rotationSpeed = 10f; // Degrees per second

  public float tiltAngle = 23.4f;   // Earth's axial tilt in degrees

  private Quaternion targetRotation;
  private NBodySimulation _script;


  void Start()
  {
    // Define the tilt as a rotation on the X-axis
    Quaternion tiltRotation = Quaternion.Euler(tiltAngle, 0, 0);

    // Apply tilt to the object's initial rotation
    transform.rotation = tiltRotation;

    // Get the NBodySimulationManager game object and find the timestep value in the script
    var manager = GameObject.FindGameObjectWithTag("NBodySimulationManager");
    if (manager != null)
    {
      _script = manager.GetComponent<NBodySimulation>();
    }
  }

  void Update()
  {
    float _rotationSpeed = rotationSpeed;
    if(_script != null)
    {
      _rotationSpeed = rotationSpeed * _script.timeStep / 100.0f;
    }

    // Calculate the next rotation step
    Quaternion deltaRotation = Quaternion.AngleAxis(_rotationSpeed * Time.deltaTime, transform.up);

    // Apply Lerp for smooth rotation
    targetRotation = deltaRotation * transform.rotation;
    transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, 0.1f); // Smooth factor (0.1 = smooth, 1.0 = instant)
  }
}
