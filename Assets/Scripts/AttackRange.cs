using System.Collections.Generic;
using UnityEngine;

public class AttackRange : MonoBehaviour
{
    public float radius = 1.5f;
    private List<GameObject> enemiesInRange = new List<GameObject>();
    void Start()
    {
        GetComponent<SphereCollider>().radius = radius;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            if (!enemiesInRange.Contains(other.gameObject))
                enemiesInRange.Add(other.gameObject);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            if (enemiesInRange.Contains(other.gameObject))
                enemiesInRange.Remove(other.gameObject);
        }
    }

    public bool IsEnemyInRange()
    {
        return enemiesInRange.Count > 0;
    }

    public GameObject[] GetEnemyInRange()
    {
        return enemiesInRange.ToArray();
    }
    public bool IsSpecificEnemyInRange(GameObject enemyToCheck)
    {
        return enemiesInRange.Contains(enemyToCheck);
    }
}
