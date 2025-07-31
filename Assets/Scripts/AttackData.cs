using UnityEngine;

[System.Serializable]
public class AttackData
{
    [Tooltip("單一一擊的名稱，如 '第一下劈砍'")]
    public string stepName;

    [Tooltip("這一擊的前搖時間")]
    public float startupTime = 0.3f;

    [Tooltip("這一擊的後搖時間")]
    public float recoveryTime = 0.5f;

    [Tooltip("這一擊的傷害")]
    public int damage = 10;

    [Header("Attack Shape")]
    [Tooltip("這一擊的攻擊距離")]
    public float attackRange = 2.0f;
    [Tooltip("這一擊的攻擊角度")]
    public float attackAngle = 90.0f;

    [Header("Movement")]
    [Tooltip("執行這一擊時向前移動的距離")]

    public float forwardMovement = 0f;
    [Tooltip("移動的時機曲線。X軸是時間進度(0-1)，Y軸是移動進度(0-1)。\n" + 
             "預設為前期不動，最後突進。")]
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}

[System.Serializable]
public class ComboData
{
    [Tooltip("連招的名稱，如 '三連斬'")]
    public string comboName;

    [Tooltip("這個連招由哪幾下攻擊組成")]
    public AttackData[] attackSteps;

    [Tooltip("使用完這套連招後的總冷卻時間")]
    public float comboCooldown = 3.0f;
}