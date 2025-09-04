using UnityEngine;

public class Player_Dodge : Player_Behavior_Dodge
{
    void Start()
    {
        transform.localPosition = new Vector3(0, 0.5f, 0);
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
