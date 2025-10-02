using UnityEngine;

public class Player : Player_Behavior
{
    void Awake()
    {
        base.Initialize();
    }

    void FixedUpdate()
    {
        base.UpdateBehavior();
    }
}
