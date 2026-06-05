using System;
using UnityEngine;

public class VR_TrainingAreaDemo : MonoBehaviour
{
    public event Action OnEpisodeEnd;

    [Header("References")]
    public VR_EnemyDemo enemyInArea;
    public VR_Player_BehaviorDemo playerInArea;

    [Header("Episode Settings")]
    private bool isReset;

    private VR_HP_Player_Demo playerHP;
    private VR_HP_Enemy enemyHP;

    void Start()
    {
        if (playerInArea != null) playerHP = playerInArea.GetComponentInChildren<VR_HP_Player_Demo>();
        if (enemyInArea != null) enemyHP = enemyInArea.GetComponentInChildren<VR_HP_Enemy>();
        isReset = false;
    }

    void FixedUpdate()
    {
        if (playerInArea == null || enemyInArea == null || isReset) return;

        // --- 判斷結束與重置條件 ---
        if (playerHP != null && playerHP.IsDead()) {
            ResetAll("Enemy"); // 玩家死
        }
        else if (enemyHP != null && enemyHP.IsDead()) {
            ResetAll("Player"); // 敵人死
        }
        else if (playerInArea.transform.position.y < -1f || enemyInArea.transform.position.y < -1f) {
            ResetAll("Other"); // 掉落地圖
        }
    }

    /// <summary>
    /// 統一重置環境與物件
    /// </summary>
    public void ResetAll(string winner)
    {
        isReset = true;
        if(winner == "Enemy")
        {
            playerInArea.gameObject.GetComponent<VR_HitEffect>().PlayDeathEffect();
        }
        else if(winner == "Player")
        {
            enemyInArea.TriggerDeath();
        }
        else
        {
            enemyInArea.TriggerDeath();
            Debug.Log("fall out");
        }
    }

    public void TriggerEpisodeEnd() => OnEpisodeEnd?.Invoke();
}