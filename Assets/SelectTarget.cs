using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SelectTarget : MonoBehaviour
{
  public string targetName = "Earth";
  public Button trackButton;
  public Slider massSlider;
  private Camera mainCamera;
  private NBodySimulation controller;

  // Start is called before the first frame update
  void Start()
  {
    if (mainCamera == null)
    {
      mainCamera = Camera.main; // Get main camera if not assigned
    }
    trackButton.onClick.AddListener(HandleClick);
    if(massSlider != null)
    {
      massSlider.onValueChanged.AddListener(delegate { HandleMassChange(); });
    }
    var obj = GameObject.FindGameObjectWithTag("NBodySimulationManager");
    controller = obj.GetComponent<NBodySimulation>();
  }

  public void HandleClick()
  {
    var objects = GameObject.FindGameObjectsWithTag("TrackingObject");
    var target = objects.FirstOrDefault(x => x.name == targetName);
    if (target == null)
      return;
    mainCamera.GetComponent<CameraManager>().targetObject = target;
  }

  public void HandleMassChange()
  {
    float sliderValue = massSlider.value / 10.0f; 
    controller.SetMass(sliderValue, targetName);
  }
}
