using UnityEngine;

public class HP_Enemy : HP
{
    private Enemy_Agent selfAgent;

    protected override void Awake()
    {
        base.Awake(); 
        selfAgent = GetComponent<Enemy_Agent>();
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