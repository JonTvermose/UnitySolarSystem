using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Assets;
using Newtonsoft.Json;
using UnityEngine;
using static NBodySimulation;

public class CoroutineJsonLoader : MonoBehaviour
{
  private const string FilePath = @"C:\Users\Jon\Downloads\dastcom5\exe\output\output_json_comet.txt";

  // Gravitational parameter of the Sun in AU^3/day^2.
  private const double mu = 0.0002959122082855911;

  private List<Body> _bodies = new List<Body>();

  void Start()
  {
    StartCoroutine(LoadJsonCoroutine());
  }

  IEnumerator LoadJsonCoroutine()
  {
    using (StreamReader sr = new StreamReader(FilePath))
    using (JsonTextReader reader = new JsonTextReader(sr))
    {
      JsonSerializer serializer = new JsonSerializer();
      int count = 0;

      while (reader.Read())
      {
        if (reader.TokenType == JsonToken.StartObject)
        {
          SmallBody data = serializer.Deserialize<SmallBody>(reader);
          ProcessData(data);
          count++;

          if (count % 100 == 0) // Yield every 100 objects
          {
            yield return null;
          }
        }
      }

      // Done parsing stuff
      // Attach the bodies to our main controller
      var obj = GameObject.FindGameObjectWithTag("NBodySimulationManager");
      var controller = obj.GetComponent<NBodySimulation>();
      if (controller != null)
      {
        controller.Bodies = _bodies.ToArray();
      }
    }
  }

  private const double G = 6.67430e-11; // Gravitational constant (m³/kg/s²)

  void ProcessData(SmallBody data)
  {
    Vector3 pos;
    Vector3 vel;
    if(float.TryParse(data.GM, out var gmParsed))
    {
      gmParsed = 0.0f;
    }
    float mass = (float)(gmParsed / G);
    ComputeStateVector(data, out pos, out vel);
    _bodies.Add(new Body { position = pos, velocity = vel, mass = mass });
  }

  /// <summary>
  /// Converts the orbital elements from a SmallBody object into a Cartesian state vector.
  /// Assumptions:
  ///  - Semi-major axis (A) is in AU.
  ///  - Eccentricity (EC) is unitless.
  ///  - Inclination (IN), Argument of Perihelion (W), Longitude of Ascending Node (OM),
  ///    and Mean Anomaly (MA) are given in degrees.
  ///  - The computed position is in AU, and the velocity in AU/day.
  /// </summary>
  void ComputeStateVector(SmallBody body, out Vector3 position, out Vector3 velocity)
  {
    // Convert angles from degrees to radians.
    double i = body.IN * Math.PI / 180.0;
    double omega = body.W * Math.PI / 180.0;
    double Omega = body.OM * Math.PI / 180.0;
    double M = body.MA * Math.PI / 180.0;

    double a = body.A;
    double e = body.EC;

    // Solve Kepler's equation for eccentric anomaly E.
    double E = M;
    const int maxIter = 100;
    const double tol = 1e-6;
    for (int iter = 0; iter < maxIter; iter++)
    {
      double f = E - e * Math.Sin(E) - M;
      double fprime = 1 - e * Math.Cos(E);
      double delta = f / fprime;
      E -= delta;
      if (Math.Abs(delta) < tol)
        break;
    }

    // Compute true anomaly fTrue.
    double fTrue = 2 * Math.Atan2(Math.Sqrt(1 + e) * Math.Sin(E / 2), Math.Sqrt(1 - e) * Math.Cos(E / 2));

    // Compute the radial distance.
    double r = a * (1 - e * Math.Cos(E));

    // Position in the orbital plane.
    double xOrb = r * Math.Cos(fTrue);
    double yOrb = r * Math.Sin(fTrue);

    // Compute specific angular momentum.
    double h = Math.Sqrt(mu * a * (1 - e * e));

    // Compute radial and transverse velocities.
    double vr = (mu / h) * e * Math.Sin(fTrue);
    double vTheta = (mu / h) * (1 + e * Math.Cos(fTrue));

    // Velocity in the orbital plane.
    double vxOrb = vr * Math.Cos(fTrue) - vTheta * Math.Sin(fTrue);
    double vyOrb = vr * Math.Sin(fTrue) + vTheta * Math.Cos(fTrue);

    // Rotate from orbital plane to heliocentric ecliptic coordinates.
    double cosOmega = Math.Cos(Omega);
    double sinOmega = Math.Sin(Omega);
    double cosomega = Math.Cos(omega);
    double sinomega = Math.Sin(omega);
    double cosi = Math.Cos(i);
    double sini = Math.Sin(i);

    // Position transformation.
    double x = xOrb * (cosOmega * cosomega - sinOmega * sinomega * cosi) - yOrb * (cosOmega * sinomega + sinOmega * cosomega * cosi);
    double y = xOrb * (sinOmega * cosomega + cosOmega * sinomega * cosi) - yOrb * (sinOmega * sinomega - cosOmega * cosomega * cosi);
    double z = xOrb * (sinomega * sini) + yOrb * (cosomega * sini);

    // Velocity transformation.
    double vx = vxOrb * (cosOmega * cosomega - sinOmega * sinomega * cosi) - vyOrb * (cosOmega * sinomega + sinOmega * cosomega * cosi);
    double vy = vxOrb * (sinOmega * cosomega + cosOmega * sinomega * cosi) - vyOrb * (sinOmega * sinomega - cosOmega * cosomega * cosi);
    double vz = vxOrb * (sinomega * sini) + vyOrb * (cosomega * sini);

    // Map the computed coordinates to Unity's coordinate system.
    // Here we assume: computed x -> Unity x, computed z -> Unity y, computed y -> Unity z.
    position = new Vector3((float)x, (float)z, (float)y);
    velocity = new Vector3((float)vx, (float)vz, (float)vy);
  }
}
