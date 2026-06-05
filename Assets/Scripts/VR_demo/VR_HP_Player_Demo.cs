using UnityEngine;

public class VR_HP_Player_Demo : HP
{
    public VR_Enemy enemyAgent;

    public override void HurtFromMelee(int damage)
    {
        CurrentHP -= damage;
        CurrentHP = Mathf.Clamp(CurrentHP, 0, maxHP);
        Debug.Log(CurrentHP);
        if (enemyAgent != null)
        {
            enemyAgent.OnDamageDealt(damage);
        }
        VR_HitEffect hitEffect = GetComponent<VR_HitEffect>();
        if (hitEffect != null)
        {
            hitEffect.PlayHitEffect();
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