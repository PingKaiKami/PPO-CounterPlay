using System;
using UnityEngine;

public class VR_TrainingAreaDemo : MonoBehaviour
{
    public event Action OnEpisodeEnd;

    [Header("Agents References")]
    public VR_EnemyDemo enemyInArea;
    public VR_Player_Agent playerAgent;

    [Header("Episode Settings")]
    public float maxEpisodeTime = 60f;
    private float episodeTimer;

    private VR_HP_Player playerHP;
    private VR_HP_Enemy enemyHP;

    void Start()
    {
        // 獲取血量腳本
        if (playerAgent != null) playerHP = playerAgent.GetComponentInChildren<VR_HP_Player>();
        if (enemyInArea != null) enemyHP = enemyInArea.GetComponentInChildren<VR_HP_Enemy>();
    }

    void FixedUpdate()
    {
        if (playerAgent == null || enemyInArea == null) return;

        episodeTimer += Time.fixedDeltaTime;

        // --- 判斷結束條件 ---
        if (playerHP != null && playerHP.IsDead()) {
            EndEntireEpisode("Enemy"); // 玩家死，敵人贏
        }
        else if (enemyHP != null && enemyHP.IsDead()) {
            EndEntireEpisode("Player"); // 敵人死，玩家贏
        }
        else if (episodeTimer >= maxEpisodeTime) {
            EndEntireEpisode("Draw");   // 平手
        }
        else if (playerAgent.transform.position.y < -1f || enemyInArea.transform.position.y < -1f) {
            EndEntireEpisode("Fall");   // 掉落地圖
        }
    }

    /// <summary>
    /// 這是統一的重置出口
    /// </summary>
    public void EndEntireEpisode(string winner)
    {
        // 1. 讓敵人結算獎勵 (在內部計算 AddReward，但不在此處呼叫 EndEpisode)
        enemyInArea.ResolveEpisode(winner, episodeTimer, maxEpisodeTime);

        // 2. 紀錄 Benchmark 數據
        if (BenchmarkManager.Instance != null)
        {
            if (playerAgent.isVerify) BenchmarkManager.Instance.RecordEpisode(playerAgent.modelName, winner, 0);
            if (enemyInArea.isVerify) BenchmarkManager.Instance.RecordEpisode(enemyInArea.modelName, winner, enemyInArea.GetCumulativeReward());
        }

        // 3. 關鍵：強迫所有 Agent 同時結束
        // 這會觸發他們各自的 OnEpisodeBegin()
        playerAgent.EndEpisode();
        enemyInArea.EndEpisode();

        // 4. 清理環境 (銷毀箭矢等)
        OnEpisodeEnd?.Invoke();

        // 5. 重置計時器
        episodeTimer = 0f;
    }

    public void TriggerEpisodeEnd() => OnEpisodeEnd?.Invoke();
}