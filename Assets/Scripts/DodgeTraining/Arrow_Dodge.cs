using UnityEngine;

public class Arrow_Dodge : MonoBehaviour
{
    public int damage;
    public float existTime = 2f;
    private float time = 0f;
    private Player_Behavior_Dodge player_Behavior;
    void Start()
    {
        player_Behavior = FindObjectOfType<Player_Behavior_Dodge>();
    }
    void Update()
    {
        if (time < existTime)
        {
            time += Time.deltaTime;
        }
        else
        {
            player_Behavior.enemy.GetComponent<Enemy_Dodge_Agent>().OnPlayerLongAttackMissed();
            Destroy(gameObject);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            HP enemy_HP = other.GetComponentInParent<HP>();
            enemy_HP.HurtFromRanged(damage);
            Destroy(gameObject);
        }
        else
        {
            player_Behavior.enemy.GetComponent<Enemy_Dodge_Agent>().OnPlayerLongAttackMissed();
            Destroy(gameObject);
        }
    }
}
