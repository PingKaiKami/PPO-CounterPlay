using UnityEngine;

public class Arrow : MonoBehaviour
{
    public int damage;
    public float existTime = 2f;
    public GameObject target;
    private float time;
    private GameObject owner; 
    void Start()
    {
        time = 0f;
    }
    private void OnEnable()
    {
        GameEvents.OnEpisodeEnd += SelfDestruct;
    }

    private void OnDisable()
    {
        GameEvents.OnEpisodeEnd -= SelfDestruct;
    }
    public void SetOwner(GameObject ownerObject)
    {
        this.owner = ownerObject;
    }
    void Update()
    {
        time += Time.deltaTime;
        if (time > existTime)
        {
            End();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            HP enemy_HP = other.GetComponentInParent<HP>();
            Enemy_Agent enemy_Agent = other.GetComponentInParent<Enemy_Agent>();
            if (enemy_Agent.curState == Enemy_Agent.EnemyState.Defending)
            {
                enemy_HP.HurtFromRanged(damage / 10);
            }
            else
            {
                enemy_HP.HurtFromRanged(damage);
            }
            owner.GetComponent<Player_Behavior>().DeregisterProjectile(gameObject);
            Destroy(gameObject);
        }
        else
        {
            End();
        }
    }
    private void End()
    {
        owner.GetComponent<Player_Behavior>().enemy.GetComponent<Enemy_Agent>().OnPlayerLongAttackMissed();
        owner.GetComponent<Player_Behavior>().DeregisterProjectile(gameObject);
        Destroy(gameObject);
    }

    private void SelfDestruct()
    {
        Destroy(gameObject);
    }
}
