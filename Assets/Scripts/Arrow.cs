using UnityEngine;

public class Arrow : MonoBehaviour
{
    public int damage;
    public float existTime = 2f;
    private float time = 0f;
    private Player_Behavior player_Behavior;
    void Start()
    {
        player_Behavior = FindObjectOfType<Player_Behavior>();
    }
    void Update()
    {
        if (time < existTime)
        {
            time += Time.deltaTime;
        }
        else
        {
            // player_Behavior.enemy.GetComponent<Enemy_Agent>().OnPlayerLongAttackMissed();
            Destroy(gameObject);
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
                enemy_HP.Hurt(damage / 10);
            }
            else
            {
                enemy_HP.Hurt(damage);
            }
            Destroy(gameObject);
        }
        else
        {
            // player_Behavior.enemy.GetComponent<Enemy_Agent>().OnPlayerLongAttackMissed();
            Destroy(gameObject);
        }
    }
}
