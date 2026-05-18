using UnityEngine;

public class SimulationSpeedController : MonoBehaviour
{
    [Range(0.1f, 50f)]
    public float simulationSpeed = 1.0f;
     void Awake()
    {
        Time.timeScale = simulationSpeed;
        
        // 保持物理步長在模擬時間下的精細度 (預設為 0.02)
        Time.fixedDeltaTime = 0.02f / simulationSpeed;
        
        // 讓渲染幀率至少跟上物理更新，避免畫面跳幀影響觀察
        Application.targetFrameRate = Mathf.RoundToInt(60 * simulationSpeed);
    }
}