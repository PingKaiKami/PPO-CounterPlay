using UnityEngine;
using UnityEngine.UI;

public abstract class HP : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] protected Slider hpSlider;
    [SerializeField] protected int maxHP = 100;
    public float CurrentHP { get; protected set; }
    public virtual void ResetHealth()
    {
        CurrentHP = maxHP;
        UpdateHPBar();
    }
    public bool IsDead()
    {
        return CurrentHP <= 0;
    }
    public int GetCurrentHealth()
    {
        return (int)CurrentHP;
    }
    public abstract void Hurt(int damage);
    protected virtual void Awake()
    {
        CurrentHP = maxHP;
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHP;
            hpSlider.value = CurrentHP;
        }
    }
    protected virtual void UpdateHPBar()
    {
        if (hpSlider != null)
        {
            hpSlider.value = CurrentHP;
        }
    }
}