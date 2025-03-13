using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoonOrbit : MonoBehaviour
{
  public Transform earth; // Reference to the Earth object
  public float orbitSpeed = 10f; // Speed of the Moon's orbit in degrees per second

  private void Update()
  {
    if (earth == null)
    {
      Debug.LogError("Earth reference not set in MoonOrbit script!");
      return;
    }

    // Rotate around the Earth
    transform.RotateAround(earth.position, Vector3.up, orbitSpeed * Time.deltaTime);

    // Keep the Moon tidally locked (always facing the Earth)
    transform.LookAt(earth.position);
  }
}
