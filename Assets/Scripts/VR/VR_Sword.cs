using System.Collections;
using System.Collections.Generic;
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
    private const float damageCooldown = 1.0f;

    // 新增：用來記錄目前在「劍刃碰撞範圍內」的所有敵人
    private HashSet<VR_HP_Enemy> enemiesInReach = new HashSet<VR_HP_Enemy>();

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
        // if(isDebug)
        //     Debug.Log($"當前角速度: {currentAngularSpeedRad:F2} rad/s");
    }

    private void OnTriggerEnter(Collider other)
    {
        // 改用 transform.root.CompareTag，確保砍到敵人的手腳子物件也能正確辨識
        if (other.CompareTag("Enemy"))
        {
            var enemyHP = other.GetComponentInParent<VR_HP_Enemy>();
            if (enemyHP != null)
            {
                // 將敵人加入清單
                enemiesInReach.Add(enemyHP);
                
                if(isDebug)
                    Debug.Log($"<color=green>敵人進入劍刃範圍：</color> {enemyHP.gameObject.name}");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            var enemyHP = other.GetComponentInParent<VR_HP_Enemy>();
            if (enemyHP != null)
            {
                // 敵人離開劍刃範圍，將其移出清單
                enemiesInReach.Remove(enemyHP);
                
                if(isDebug)
                    Debug.Log($"<color=yellow>敵人離開劍刃範圍：</color> {enemyHP.gameObject.name}");
            }
        }
    }

    void Update()
    {
        // 1. 如果清單內沒有敵人，直接 return 節省效能
        if (enemiesInReach.Count == 0) return;

        // 2. 防呆機制：如果敵人在範圍內死亡並被 Destroy，將其從清單中剔除避免報錯
        enemiesInReach.RemoveWhere(enemy => enemy == null);
        if (enemiesInReach.Count == 0) return;

        // 3. 核心判定：當劍在敵人體內，檢查玩家的揮砍力道 與 冷卻時間
        if (currentAngularSpeedRad >= minAngularSpeedRad && Time.time > lastDamageTime + damageCooldown)
        {
            if (isDebug)
                Debug.Log($"<color=cyan>有效揮砍！</color> 弧度速: {currentAngularSpeedRad:F2} rad/s，命中 {enemiesInReach.Count} 個目標");

            // 4. 對範圍內所有敵人造成傷害
            foreach (var enemy in enemiesInReach)
            {
                if (enemy != null) 
                {
                    enemy.HurtFromMelee(player.damage);
                }
            }

            // 5. 刷新冷卻時間
            lastDamageTime = Time.time;
        }
        
        // 註：這裡拿掉了原本「力道不足」的 Debug.Log，
        // 因為 Update 每一幀都會執行，如果劍停在敵人體內會導致 Debug 狂洗版。
    }
}