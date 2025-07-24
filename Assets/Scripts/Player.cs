using UnityEngine;

public class Player : Player_Behavior
{
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        attackRange = GetComponentInChildren<AttackRange>();
        attackRadius = attackRange.radius;
        mat = GetComponentInChildren<Renderer>().material;
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
