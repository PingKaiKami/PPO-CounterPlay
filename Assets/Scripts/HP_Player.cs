using UnityEngine;

public class HP_Player : HP
{
    public Enemy_Agent enemyAgent;

    public override void Hurt(int damage)
    {
        CurrentHP -= damage;
        CurrentHP = Mathf.Clamp(CurrentHP, 0, maxHP);
        if (enemyAgent != null)
        {
            enemyAgent.OnDamageDealt(damage);
        }
        UpdateHPBar();
    }
}