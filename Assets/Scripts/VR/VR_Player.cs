using UnityEngine;

public class VR_Player : VR_Player_Behavior
{
    void Awake()
    {
        base.Initialize();
    }

    void FixedUpdate()
    {
        base.UpdateBehavior();
        // 紀錄數據
        base.PerformDataLogging();
    }
}
