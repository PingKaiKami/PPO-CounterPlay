using UnityEngine;

public class AttackRange_Enemy : MonoBehaviour
{
    public float currentRadius = 1.5f;
    public float currentAngle = 90.0f;

    private SphereCollider detectionCollider;

    void Awake()
    {
        detectionCollider = GetComponent<SphereCollider>();
        if (detectionCollider == null)
        {
            detectionCollider = gameObject.AddComponent<SphereCollider>();
        }
        detectionCollider.isTrigger = true;
        detectionCollider.radius = currentRadius;
    }

    public void UpdateAttackShape(AttackData attackData)
    {
        currentRadius = attackData.attackRange;
        currentAngle = attackData.attackAngle;

        detectionCollider.radius = currentRadius;
    }
    public bool IsPlayerInRange()
    {
        Collider[] collidersInRange = Physics.OverlapSphere(transform.position, currentRadius);

        foreach (Collider col in collidersInRange)
        {
            if (col.CompareTag("Player"))
            {
                Vector3 directionToPlayer = (col.transform.position - transform.position).normalized;
                
                Vector3 enemyForward = transform.parent.forward;

                if (Vector3.Angle(enemyForward, directionToPlayer) < currentAngle / 2)
                {
                    return true;
                }
            }
        }

        return false;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 forward = transform.parent != null ? transform.parent.forward : transform.forward;

        Quaternion leftRayRotation = Quaternion.AngleAxis(-currentAngle / 2, Vector3.up);
        Quaternion rightRayRotation = Quaternion.AngleAxis(currentAngle / 2, Vector3.up);
        Vector3 leftRayDirection = leftRayRotation * forward;
        Vector3 rightRayDirection = rightRayRotation * forward;

        Gizmos.DrawRay(transform.position, leftRayDirection * currentRadius);
        Gizmos.DrawRay(transform.position, rightRayDirection * currentRadius);

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1, 0, 0, 0.1f);
        UnityEditor.Handles.DrawSolidArc(transform.position, Vector3.up, leftRayDirection, currentAngle, currentRadius);
#endif
    }
}