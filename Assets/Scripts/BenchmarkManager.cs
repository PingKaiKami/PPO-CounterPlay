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

    [Header("測試設定")]
    [Tooltip("每個模型測試的最大回合數，達到後該模型不再紀錄數據")]
    public int maxEpisodesPerModel = 1000;

    [HideInInspector]
    public List<ModelStats> currentStats = new List<ModelStats>();
    
    [TextArea(10, 20)]
    public string liveSummary;

    private Dictionary<string, ModelStats> statsLookup = new Dictionary<string, ModelStats>();
    private bool _isDirty = false;
    private float _lastUpdateTime;
    private bool _isPausedByBenchmark = false;

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

        // 如果遊戲已經因為測試結束而暫停，直接攔截不紀錄
        if (_isPausedByBenchmark) return;

        if (!statsLookup.ContainsKey(modelName))
        {
            ModelStats newStats = new ModelStats { modelName = modelName };
            statsLookup.Add(modelName, newStats);
            currentStats.Add(newStats);
        }

        ModelStats stats = statsLookup[modelName];

        // 👈 條件一：當某一種模型到達該回合數，則不再紀錄該模型數據
        if (stats.totalEpisodes >= maxEpisodesPerModel)
        {
            return; 
        }

        stats.totalEpisodes++;
        stats.accumulatedReward += finalReward;

        if (winner == "Enemy") stats.enemyWins++;
        else if (winner == "Draw") stats.draws++;

        _isDirty = true;

        // 👈 條件二：檢查是否所有已註冊的模型都達到了回合數上限
        CheckAndPauseGame();
    }

    private void CheckAndPauseGame()
    {
        // 如果目前根本還沒有注入任何模型數據，先跳過
        if (statsLookup.Count == 0) return;

        bool allModelsFinished = true;
        foreach (var pair in statsLookup)
        {
            if (pair.Value.totalEpisodes < maxEpisodesPerModel)
            {
                allModelsFinished = false;
                break;
            }
        }

        // 當所有已知模型（例如 old 和 new 兩種）都到達上限，則暫停遊戲
        if (allModelsFinished && !_isPausedByBenchmark)
        {
            _isPausedByBenchmark = true;
            UpdateVisualSummary(); // 確保畫面上是最終最精準的數據
            
            #if UNITY_EDITOR
                // 這行等同於程式自動幫你點擊 Unity 編輯器正上方的 Pause 按鈕！
                UnityEditor.EditorApplication.isPaused = true;
            #else
                Time.timeScale = 0f; // 如果打包成執行檔(Build)，就切換回時間凍結
            #endif
            
            Debug.LogWarning($"<color=yellow>【Benchmark 測試結束】</color> 所有模型皆已達到 {maxEpisodesPerModel} 回合！遊戲已暫停。");
        }
    }

    private void UpdateVisualSummary()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== 訓練即時戰報 ===");
        foreach (var s in currentStats)
        {
            string status = s.totalEpisodes >= maxEpisodesPerModel ? " [已完賽]" : " [測試中]";
            sb.AppendLine(s.GetSummary() + status);
        }
        
        if (_isPausedByBenchmark)
        {
            sb.AppendLine("\n>>> 測試已全部結束，遊戲暫停中 <<<");
        }
        
        liveSummary = sb.ToString();
    }

    [ContextMenu("Reset Stats")]
    public void ResetAll()
    {
        currentStats.Clear();
        statsLookup.Clear();
        liveSummary = "";
        _isPausedByBenchmark = false;
        Time.timeScale = 1f;
    }
}