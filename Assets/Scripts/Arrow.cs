using UnityEngine;

public class Arrow : MonoBehaviour
{
    public int damage;
    public float existTime = 2f;
    public GameObject target;
    private float time = 0f;
    private Player_Behavior player_Behavior;
    private float distance2Target;
    private float formedDistance;
    void Start()
    {
        distance2Target = 999;
        formedDistance = distance2Target;
        player_Behavior = FindObjectOfType<Player_Behavior>();
    }
    void Update()
    {
        if (time < 0.1f)
        {
            time += Time.deltaTime;
        }
        else
        {
            distance2Target = Vector3.Distance(target.transform.position, transform.position);
            if (formedDistance < distance2Target)
            {
                Debug.Log("destroy arrow");
                player_Behavior.enemy.GetComponent<Enemy_Agent>().OnPlayerLongAttackMissed();
                Destroy(gameObject);
            }
            formedDistance = distance2Target;
            time = 0f;
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
            Destroy(gameObject);
        }
        else
        {
            player_Behavior.enemy.GetComponent<Enemy_Agent>().OnPlayerLongAttackMissed();
            Destroy(gameObject);
        }
    }
}
