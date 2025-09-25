using UnityEngine;

public class Player : Player_Behavior
{
    void Awake()
    {
        base.Initialize(); // 调用基类的初始化
    }

    void FixedUpdate() // 建议在 FixedUpdate 中更新行为，与物理同步
    {
        base.UpdateBehavior(); // 调用基类的核心更新循环
    }
}
