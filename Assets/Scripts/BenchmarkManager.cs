using UnityEngine;
using System.Collections.Generic;
using System.Text;

public class BenchmarkManager : MonoBehaviour
{
    public static BenchmarkManager Instance { get; private set; }

    [System.Serializable]
    public class ModelStats
    {
        public string modelName;
        public int totalEpisodes;
        public int enemyWins;
        public int draws;
        public float accumulatedReward;

        public string GetSummary()
        {
            float winRate = totalEpisodes > 0 ? ((float)enemyWins / totalEpisodes) * 100f : 0;
            float avgReward = totalEpisodes > 0 ? accumulatedReward / totalEpisodes : 0;
            return $"{modelName}: Eps:{totalEpisodes} | Win:{winRate:F1}% | Draw:{draws} | AvgRew:{avgReward:F2}";
        }
    }

    // 使用 HideInInspector 讓 Unity 不要去畫這個容易崩潰的清單
    [HideInInspector]
    public List<ModelStats> currentStats = new List<ModelStats>();
    
    // 改用這個來在 Inspector 看結果
    [TextArea(10, 20)]
    public string liveSummary;

    private Dictionary<string, ModelStats> statsLookup = new Dictionary<string, ModelStats>();
    private bool _isDirty = false;
    private float _lastUpdateTime;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (_isDirty && Time.realtimeSinceStartup - _lastUpdateTime > 1.0f)
        {
            UpdateVisualSummary();
            _isDirty = false;
            _lastUpdateTime = Time.realtimeSinceStartup;
        }
    }

    public void RecordEpisode(string modelName, string winner, float finalReward)
    {
        if (string.IsNullOrEmpty(modelName)) return;

        if (!statsLookup.ContainsKey(modelName))
        {
            ModelStats newStats = new ModelStats { modelName = modelName };
            statsLookup.Add(modelName, newStats);
            currentStats.Add(newStats);
        }

        ModelStats stats = statsLookup[modelName];
        stats.totalEpisodes++;
        stats.accumulatedReward += finalReward;

        if (winner == "Enemy") stats.enemyWins++;
        else if (winner == "Draw") stats.draws++;

        _isDirty = true;
    }

    private void UpdateVisualSummary()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== 訓練即時戰報 ===");
        foreach (var s in currentStats)
        {
            sb.AppendLine(s.GetSummary());
        }
        liveSummary = sb.ToString();
    }

    [ContextMenu("Reset Stats")]
    public void ResetAll()
    {
        currentStats.Clear();
        statsLookup.Clear();
        liveSummary = "";
    }
}