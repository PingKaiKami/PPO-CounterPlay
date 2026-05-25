using System;
using UnityEngine;

public class VR_TrainingAreaDemo : MonoBehaviour
{
    public event Action OnEpisodeEnd;

    [Header("References")]
    public VR_EnemyDemo enemyInArea;
    public VR_Player_BehaviorDemo playerInArea;

    [Header("Episode Settings")]
    public float maxEpisodeTime = 60f;
    private float episodeTimer;

    private VR_HP_Player playerHP;
    private VR_HP_Enemy enemyHP;

    void Start()
    {
        if (playerInArea != null) playerHP = playerInArea.GetComponentInChildren<VR_HP_Player>();
        if (enemyInArea != null) enemyHP = enemyInArea.GetComponentInChildren<VR_HP_Enemy>();
    }

    void FixedUpdate()
    {
        if (playerInArea == null || enemyInArea == null) return;

        episodeTimer += Time.fixedDeltaTime;

        // --- 判斷結束與重置條件 ---
        if (playerHP != null && playerHP.IsDead()) {
            ResetAll(); // 玩家死
        }
        else if (enemyHP != null && enemyHP.IsDead()) {
            ResetAll(); // 敵人死
        }
        else if (episodeTimer >= maxEpisodeTime) {
            ResetAll(); // 時間到平手
        }
        else if (playerInArea.transform.position.y < -1f || enemyInArea.transform.position.y < -1f) {
            ResetAll(); // 掉落地圖
        }
    }

    /// <summary>
    /// 統一重置環境與物件
    /// </summary>
    public void ResetAll()
    {
        enemyInArea.ResetEnemy();
        playerInArea.ResetStateToIdle();

        OnEpisodeEnd?.Invoke();

        episodeTimer = 0f;
    }

    public void TriggerEpisodeEnd() => OnEpisodeEnd?.Invoke();
}