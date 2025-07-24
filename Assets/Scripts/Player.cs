using UnityEngine;

public class Player : Player_Behavior
{
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        attackRange = GetComponentInChildren<AttackRange>();
        attackRadius = attackRange.radius;
        mat = GetComponentInChildren<Renderer>().material;
        SetRandomTime();
    }
    void Update()
    {
        Move();
        if (curState == PlayerState.Waiting)
        {
            Wait();
        }
    }
}
