using UnityEngine;

public class SimulationSpeedController : MonoBehaviour
{
    [Range(0.1f, 50f)]
    public float simulationSpeed = 1.0f;
     void Awake()
    {
        Time.timeScale = simulationSpeed;
        Application.targetFrameRate = 50;
    }

}