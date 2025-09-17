using UnityEngine;

public class HP_Enemy : HP
{
    private Enemy_Agent selfAgent;

    protected override void Awake()
    {
        base.Awake();
        selfAgent = GetComponent<Enemy_Agent>();
    }
    public override void HurtFromMelee(int damage)
    {
        CurrentHP -= damage;
        CurrentHP = Mathf.Clamp(CurrentHP, 0, maxHP);

        if (selfAgent != null)
        {
            selfAgent.OnDamageTakenFromMelee(damage);
        }

        UpdateHPBar();
    }
    public override void HurtFromRanged(int damage)
    {
        CurrentHP -= damage;
        CurrentHP = Mathf.Clamp(CurrentHP, 0, maxHP);
        
        if (selfAgent != null)
        {
            selfAgent.OnDamageTakenFromRanged(damage);
        }

        UpdateHPBar();
    }
}