using UnityEngine;

public class Arrow : MonoBehaviour
{
    public int damage;
    public float existTime = 3f;
    public GameObject target;
    private float time;
    private GameObject owner; 
    private Rigidbody rb;
    private Vector3 lastPosition;
    private TrainingArea myArea;
    void Start()
    {
        time = 0f;
    }
    private void OnEnable()
    {
        rb = GetComponent<Rigidbody>();
        lastPosition = transform.position;
        myArea = GetComponentInParent<TrainingArea>();

        if (myArea != null)
        {
            myArea.OnEpisodeEnd += SelfDestruct;
        }
    }

    private void OnDisable()
    {
        if (myArea != null)
        {
            myArea.OnEpisodeEnd -= SelfDestruct;
        }
    }
    public void SetOwner(GameObject ownerObject)
    {
        this.owner = ownerObject;
    }
    void FixedUpdate()
    {
        time += Time.fixedDeltaTime;

        // --- 物理穿透檢查 (Raycast Sweep) ---
        Vector3 currentPosition = transform.position;
        Vector3 direction = currentPosition - lastPosition;
        float distance = direction.magnitude;

        if (distance > 0)
        {
            if (Physics.Raycast(lastPosition, direction, out RaycastHit hit, distance))
            {
                if (hit.collider.CompareTag("Enemy"))
                {
                    HandleHit(hit.collider);
                    return;
                }
                else if (!hit.collider.isTrigger) // 撞到牆壁等障礙物
                {
                    End();
                    return;
                }
            }
        }
        lastPosition = currentPosition;

        if (time > existTime)
        {
            End();
        }
    }

    private void HandleHit(Collider other)
    {
        HP enemy_HP = other.GetComponentInParent<HP>();
        Enemy_Agent enemy_Agent = other.GetComponentInParent<Enemy_Agent>();
        
        if (enemy_HP != null && enemy_Agent != null)
        {
            int finalDamage = (enemy_Agent.curState == Enemy_Agent.EnemyState.Defending) ? damage / 10 : damage;
            enemy_HP.HurtFromRanged(finalDamage);
        }

        if (owner != null) owner.GetComponent<Player_Behavior>().DeregisterProjectile(gameObject);
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 保留作為備援，但主要邏輯移至 HandleHit
        if (other.CompareTag("Enemy")) HandleHit(other);
        else if (!other.isTrigger) End();
    }

    private void End()
    {
        if (owner != null)
        {
            var pb = owner.GetComponent<Player_Behavior>();
            pb.enemy?.GetComponent<Enemy_Agent>()?.OnPlayerLongAttackMissed();
            pb.DeregisterProjectile(gameObject);
        }
        Destroy(gameObject);
    }

    private void SelfDestruct()
    {
        Destroy(gameObject);
    }
}
