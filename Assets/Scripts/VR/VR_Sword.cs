using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class VR_Sword : MonoBehaviour
{
    public Transform swordTip;

    [Header("Damage Settings (Radians)")]
    [Tooltip("最低揮擊角速度 (弧度/秒)。建議範圍：6.0 ~ 10.0 (約 350~600 度)")]
    public float minAngularSpeedRad = 4.5f; 
    public VR_Player player;
    public bool isDebug;

    private Quaternion lastRotation;
    private float currentAngularSpeedRad; // 單位：Radians per second
    private float lastDamageTime;
    private const float damageCooldown = 0.5f;

    public float GetAngularSpeedRad() => currentAngularSpeedRad;

    void Start()
    {
        lastRotation = transform.rotation;
    }

    void FixedUpdate()
    {
        // 1. 取得兩幀之間的角度差異 (Unity 預設回傳 Degrees)
        float angleDiffDeg = Quaternion.Angle(lastRotation, transform.rotation);

        // 2. 轉換為弧度 (Degrees to Radians)
        float angleDiffRad = angleDiffDeg * Mathf.Deg2Rad;

        // 3. 計算角速度 (弧度 / 秒)
        currentAngularSpeedRad = angleDiffRad / Time.fixedDeltaTime;

        // 4. 更新紀錄
        lastRotation = transform.rotation;

        // Debug 用：方便調整數值
        if(isDebug)
            Debug.Log($"當前角速度: {currentAngularSpeedRad:F2} rad/s");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy") && Time.time > lastDamageTime + damageCooldown)
        {
            // 核心判定
            if (currentAngularSpeedRad >= minAngularSpeedRad)
            {
                if(isDebug)
                    Debug.Log($"<color=cyan>有效揮砍！</color> 弧度速: {currentAngularSpeedRad:F2} rad/s");
                
                var enemyHP = other.GetComponentInParent<VR_HP_Enemy>();
                if (enemyHP != null)
                {
                    lastDamageTime = Time.time;
                    // 直接從 player 獲取最新傷害值
                    enemyHP.HurtFromMelee(player.damage);
                }
            }
            else
            {
                if(isDebug)
                    Debug.Log($"<color=red>力道不足：</color> {currentAngularSpeedRad:F2} rad/s");
            }
        }
    }
}
