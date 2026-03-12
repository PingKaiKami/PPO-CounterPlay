using UnityEngine;

public class VR_HP_Enemy : HP
{
    private VR_Enemy selfAgent;

    protected override void Awake()
    {
        base.Awake();
        selfAgent = GetComponent<VR_Enemy>();
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