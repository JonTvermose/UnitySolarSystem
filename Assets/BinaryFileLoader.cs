using Assets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static NBodySimulation;

public class BinaryFileLoader : MonoBehaviour
{
  public Button resetEverythingButton;

  // Start is called before the first frame update
  void Start()
  {
    resetEverythingButton.onClick.AddListener(Initialize);
    Initialize();
  }

  private void Initialize()
  {
    var comets = BinaryDataReader.LoadData(@"C:\Users\Jon\Downloads\dastcom5\exe\output\bodies.dat");
    for (int i = 0; i < comets.Length; i++)
    {
      comets[i].isComet = 1.0f;
    }

    var numbered_asteroids = BinaryDataReader.LoadData(@"C:\Users\Jon\Downloads\dastcom5\exe\output\numbered_asteroids.dat");
    var unnumbered_asteroids = BinaryDataReader.LoadData(@"C:\Users\Jon\Downloads\dastcom5\exe\output\unnumbered_asteroids.dat");
    var planets = GetSolarSystemBodies(); // BinaryDataReader.LoadData(@"C:\Users\Jon\Downloads\dastcom5\exe\output\planets.dat");    

    // Remove the first asteroid from numbered_asteroids (if needed)
    var newNumberedAsteroids = new Body[numbered_asteroids.Length - 1];
    Array.Copy(numbered_asteroids, 1, newNumberedAsteroids, 0, newNumberedAsteroids.Length);
    numbered_asteroids = newNumberedAsteroids;

    var bodies = new Body[comets.Length + numbered_asteroids.Length + unnumbered_asteroids.Length];
    comets.CopyTo(bodies, 0);
    numbered_asteroids.CopyTo(bodies, comets.Length);
    unnumbered_asteroids.CopyTo(bodies, comets.Length + numbered_asteroids.Length);

    // Attach the bodies to our main controller
    var obj = GameObject.FindGameObjectWithTag("NBodySimulationManager");
    var controller = obj.GetComponent<NBodySimulation>();
    for(int i = 0; i < bodies.Length; i++)
    {
      bodies[i].collided = -1.0f;
    }
    if (controller != null)
    {
      controller.Bodies = bodies;
      controller.MajorBodies = planets; // todo add more major bodies like ceres, etc
    }
    controller.Initialize();

    Debug.Log("Simulation initialized.");
  }

  public static Body[] GetSolarSystemBodies()
  {
    var bodies = new List<Body>();

    // Sun (center of the coordinate system)
    bodies.Add(new Body
    {
      position = new Vector3(0, 0, 0), // AU
      velocity = new Vector3(0, 0, 0), // m/s
      mass = 1.989e30f // kg
    });

    // Planets
    bodies.Add(new Body { position = new Vector3(0.3871f, 0, 0), velocity = new Vector3(0, 0, 47360), mass = 3.3011e23f, collided = 0 }); // Mercury
    bodies.Add(new Body { position = new Vector3(0.7233f, 0, 0), velocity = new Vector3(0, 0, 35020), mass = 4.8675e24f, collided = 0 }); // Venus
    bodies.Add(new Body { position = new Vector3(1.0000f, 0, 0), velocity = new Vector3(0, 0, 29780), mass = 5.972e24f, collided = 0 }); // Earth
    bodies.Add(new Body { position = new Vector3(1.5237f, 0, 0), velocity = new Vector3(0, 0, 24077), mass = 6.4171e23f, collided = 0 }); // Mars
    bodies.Add(new Body { position = new Vector3(5.2028f, 0, 0), velocity = new Vector3(0, 0, 13070), mass = 1.8982e27f, collided = 0 }); // Jupiter
    bodies.Add(new Body { position = new Vector3(9.5388f, 0, 0), velocity = new Vector3(0, 0, 9690), mass = 5.6834e26f, collided = 0 }); // Saturn
    bodies.Add(new Body { position = new Vector3(19.1914f, 0, 0), velocity = new Vector3(0, 0, 6810), mass = 8.6810e25f, collided = 0 }); // Uranus
    bodies.Add(new Body { position = new Vector3(30.0611f, 0, 0), velocity = new Vector3(0, 0, 5430), mass = 1.0241e26f, collided = 0 }); // Neptune
    bodies.Add(new Body { position = new Vector3(39.4821f, 0, 0), velocity = new Vector3(0, 0, 4740), mass = 1.303e22f, collided = 0 }); // Pluto
    bodies.Add(new Body { position = new Vector3(1.01f, -0.27f, -2.72f), velocity = new Vector3(15932.28f, -2774.08f, 5157.77f), mass = 9.383516e18f, collided = 0 }); // Ceres

    return bodies.ToArray();
  }

  // Update is called once per frame
  void Update()
  {

  }
}
