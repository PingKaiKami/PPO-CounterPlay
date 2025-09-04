using UnityEngine;

public class HP_Enemy_Dodge : HP
{
    private Enemy_Dodge_Agent selfAgent;

    protected override void Awake()
    {
        base.Awake(); 
        selfAgent = GetComponent<Enemy_Dodge_Agent>();
    }
    public override void Hurt(int damage)
    {
        CurrentHP -= damage;
        CurrentHP = Mathf.Clamp(CurrentHP, 0, maxHP);
        
        if (selfAgent != null)
        {
            selfAgent.OnDamageTaken(damage);
        }

        UpdateHPBar();
    }
}