using UnityEngine;
using System.Collections.Generic;

public class BenchmarkManager : MonoBehaviour
{
    public static BenchmarkManager Instance { get; private set; }

    [System.Serializable]
    public class ModelStats
    {
        [Header("Identity")]
        public string modelName;

        [Header("Raw Data")]
        public int totalEpisodes;
        public int enemyWins;
        [HideInInspector] 
        public float accumulatedReward; // 總分累加

        [Header("Calculated Metrics (Read Only)")]
        public string meanRewardDisplay;        // 平均獎勵 (accumulatedReward / totalEpisodes)
        public float winRatePercent;  // 勝率百分比

        // 用來更新顯示數值的函數
        public void Recalculate()
        {
            float mean;
            if (totalEpisodes > 0)
            {
                mean = accumulatedReward / totalEpisodes;
                winRatePercent = ((float)enemyWins / totalEpisodes) * 100f;
            }
            else
            {
                mean = 0;
                winRatePercent = 0;
            }

            if (modelName.Contains("Player"))
            {
                meanRewardDisplay = "N/A (Imitation)";
            }
            else
            {
                meanRewardDisplay = mean.ToString("F3");
            }
        }
    }

    // 這一條 List 會顯示在 Inspector 上讓你即時監控
    [Header("Live Statistics")]
    public List<ModelStats> currentStats = new List<ModelStats>();

    // 這一條 Dictionary 用來快速查找 (程式邏輯用，Inspector 看不到)
    private Dictionary<string, ModelStats> statsLookup = new Dictionary<string, ModelStats>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RecordEpisode(string modelName, string winner, float finalReward)
    {
        ModelStats stats;

        // 1. 如果這個模型是第一次出現，建立新資料並加入 List 和 Dictionary
        if (!statsLookup.ContainsKey(modelName))
        {
            stats = new ModelStats { modelName = modelName };
            statsLookup.Add(modelName, stats);
            currentStats.Add(stats); // 加到 List 讓 Inspector 顯示
        }
        else
        {
            stats = statsLookup[modelName];
        }

        // 2. 更新原始數據
        stats.totalEpisodes++;
        stats.accumulatedReward += finalReward;

        if (winner == "Enemy")
        {
            stats.enemyWins++;
        }

        // 3. 重新計算平均值 (為了更新 Inspector 顯示)
        stats.Recalculate();
    }

    // 提供一個按鈕或方法手動重置
    [ContextMenu("Reset Stats")]
    public void ResetAll()
    {
        currentStats.Clear();
        statsLookup.Clear();
    }
}