using UnityEngine;

[System.Serializable]
public class AttackData
{
    [Tooltip("攻擊模式的名稱，方便識別")]
    public string attackName;

    [Tooltip("攻擊的前搖時間")]
    public float startUpTime = 0.5f;

    [Tooltip("攻擊造成的傷害")]
    public int damage = 10;

    [Tooltip("攻擊的後搖時間")]
    public float recoveryTime = 1.0f;
    
    [Tooltip("攻擊的範圍")]
    public float attackRange; // 這次攻擊的範圍
}