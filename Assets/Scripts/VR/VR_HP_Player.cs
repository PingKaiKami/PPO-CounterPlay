using UnityEngine;

public class VR_HP_Player : HP
{
    public VR_Enemy enemyAgent;

    public override void HurtFromMelee(int damage)
    {
        CurrentHP -= damage;
        CurrentHP = Mathf.Clamp(CurrentHP, 0, maxHP);
        if (enemyAgent != null)
        {
            enemyAgent.OnDamageDealt(damage);
        }
        UpdateHPBar();
    }
    public override void HurtFromRanged(int damage)
    {
        CurrentHP -= damage;
        CurrentHP = Mathf.Clamp(CurrentHP, 0, maxHP);
        
        if (enemyAgent != null)
        {
            enemyAgent.OnDamageTakenFromRanged(damage);
        }

        UpdateHPBar();
    }
}