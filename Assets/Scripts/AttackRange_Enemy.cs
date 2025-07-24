using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackRange_Enemy : MonoBehaviour
{
    private float radius;
    private bool isHit = false;
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isHit = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isHit = false;
        }
    }
    public bool isInRange()
    {
        return isHit;
    }

    public void changeRange(AttackData attackData)
    {
        radius = attackData.attackRange;
        GetComponent<SphereCollider>().radius = radius;
    }
}
