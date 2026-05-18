using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackRange : MonoBehaviour
{
    [Header("Attack Shape Settings")]
    [Tooltip("攻擊的距離")]
    public float radius = 2.0f;
    [Tooltip("攻擊的角度")]
    [Range(0, 360)]
    public float angle = 90.0f;

    private SphereCollider detectionCollider;
    private Collider[] results = new Collider[10]; // 預配置快取

    void Awake()
    {
        detectionCollider = GetComponent<SphereCollider>();
        if (detectionCollider == null)
        {
            detectionCollider = gameObject.AddComponent<SphereCollider>();
        }
        detectionCollider.isTrigger = true;
        detectionCollider.radius = radius;
    }
    
    private void OnValidate()
    {
        if (detectionCollider == null)
        {
            detectionCollider = GetComponent<SphereCollider>();
        }
        if (detectionCollider != null)
        {
            detectionCollider.radius = radius;
        }
    }

    public bool IsEnemyInRange()
    {
        return FindEnemiesInFanRange().Count > 0;
    }

    public GameObject[] GetEnemyInRange()
    {
        return FindEnemiesInFanRange().ToArray();
    }
    
    public bool IsSpecificEnemyInRange(GameObject enemyToCheck)
    {
        if (enemyToCheck == null) return false;

        if (Vector3.Distance(transform.position, enemyToCheck.transform.position) > radius)
        {
            return false;
        }

        Vector3 directionToEnemy = (enemyToCheck.transform.position - transform.position).normalized;
        
        Vector3 playerForward = transform.parent != null ? transform.parent.forward : transform.forward;
        
        if (Vector3.Angle(playerForward, directionToEnemy) < angle / 2)
        {
            return true;
        }

        return false;
    }

    private List<GameObject> FindEnemiesInFanRange()
    {
        List<GameObject> enemiesInFan = new List<GameObject>();
        
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, results);

        for (int i = 0; i < count; i++)
        {
            Collider col = results[i];
            if (col.CompareTag("Enemy"))
            {
                Vector3 directionToEnemy = (col.transform.position - transform.position).normalized;
                
                Vector3 playerForward = transform.parent != null ? transform.parent.forward : transform.forward;

                if (Vector3.Angle(playerForward, directionToEnemy) < angle / 2)
                {
                    enemiesInFan.Add(col.gameObject);
                }
            }
        }
        return enemiesInFan;
    }

    // 視覺化 Gizmo
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Vector3 forward = transform.parent != null ? transform.parent.forward : transform.forward;

        Quaternion leftRayRotation = Quaternion.AngleAxis(-angle / 2, Vector3.up);
        Quaternion rightRayRotation = Quaternion.AngleAxis(angle / 2, Vector3.up);
        Vector3 leftRayDirection = leftRayRotation * forward;
        Vector3 rightRayDirection = rightRayRotation * forward;

        Gizmos.DrawRay(transform.position, leftRayDirection * radius);
        Gizmos.DrawRay(transform.position, rightRayDirection * radius);

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(0, 1, 0, 0.1f);
        UnityEditor.Handles.DrawSolidArc(transform.position, Vector3.up, leftRayDirection, angle, radius);
#endif
    }
}