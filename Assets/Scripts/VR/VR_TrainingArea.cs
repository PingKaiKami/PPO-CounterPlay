using System;
using UnityEngine;

public class VR_TrainingArea : MonoBehaviour
{
    public event Action OnEpisodeEnd;

    [Header("Agents References")]
    public VR_Enemy enemyInArea;
    public VR_Player_Agent playerAgent;

    [Header("Episode Settings")]
    public float maxEpisodeTime = 60f;
    private float episodeTimer;

    [Header("Auto Stop Settings")]
    public bool AutoStop = true;       // 是否開啟自動停止
    public int maxEpisodes = 1000;     // 目標 Episode 數量
    private int currentEpisodeCount = 0; // 當前 Episode 計數器

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

        // 6. 處理自動停止邏輯
        currentEpisodeCount++;
        if (AutoStop)
        {
            Debug.Log($"[訓練進度] Episode: {currentEpisodeCount} / {maxEpisodes} | 結果: {winner}");
            if(currentEpisodeCount >= maxEpisodes)
            {
                Debug.Log("達到設定的最大 Episode 數量，自動停止訓練！");
                
                // 根據是否在 Unity 編輯器環境中，執行不同的停止指令
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false; // 停止 Unity 編輯器播放
#else
                Application.Quit(); // 如果是打包出來的執行檔 (Build)，則關閉程式
#endif
            }
        }
    }

    public void TriggerEpisodeEnd() => OnEpisodeEnd?.Invoke();
}