using System;
using UnityEngine;

public class TrainingArea : MonoBehaviour
{
    public event Action OnEpisodeEnd;
    
    [Header("Agents")]
    public Enemy_Agent enemyInArea;
    public Player_Behavior playerInArea;
    public Player_Agent playerAgent;

    [Header("Episode Control")]
    public float maxEpisodeTime = 60f;
    private float episodeTimer;

    private HP playerHP;
    private HP enemyHP;

    void Start()
    {
        // 自動獲取引用
        if (enemyInArea == null) enemyInArea = GetComponentInChildren<Enemy_Agent>();
        if (playerAgent == null) playerAgent = GetComponentInChildren<Player_Agent>();
        
        if (playerAgent != null) playerHP = playerAgent.GetComponent<HP>();
        if (enemyInArea != null) enemyHP = enemyInArea.GetComponent<HP>();
    }

    void FixedUpdate()
    {
        if (enemyInArea == null || playerAgent == null || playerHP == null || enemyHP == null) return;

        episodeTimer += Time.fixedDeltaTime;

        // 1. 玩家死亡 -> 敵人獲勝
        if (playerHP.IsDead())
        {
            EndEpisodeWithResult("Enemy");
            return;
        }

        // 2. 敵人死亡 -> 玩家獲勝
        if (enemyHP.IsDead())
        {
            EndEpisodeWithResult("Player");
            return;
        }

        // 3. 超時
        if (episodeTimer >= maxEpisodeTime)
        {
            EndEpisodeWithResult("Draw");
            return;
        }

        // 4. 掉出地圖
        if (playerAgent.transform.localPosition.y < -5f)
        {
            EndEpisodeWithResult("Fall");
            return;
        }
        
        if (enemyInArea.transform.localPosition.y < -5f)
        {
            EndEpisodeWithResult("Player"); // 敵人掉下去視同玩家贏
            return;
        }
    }

    private void EndEpisodeWithResult(string result)
    {
        // 1. 讓敵人 Agent 根據結果結算獎勵
        if (enemyInArea != null)
        {
            enemyInArea.ResolveEpisode(result, episodeTimer, maxEpisodeTime);
        }

        // 2. 記錄數據到 BenchmarkManager
        if (BenchmarkManager.Instance != null)
        {
            if (playerAgent != null && playerAgent.isVerify) 
                BenchmarkManager.Instance.RecordEpisode(playerAgent.modelName, result, 0);
            
            if (enemyInArea != null && enemyInArea.isVerify)
                BenchmarkManager.Instance.RecordEpisode(enemyInArea.modelName, result, enemyInArea.GetCumulativeReward());
        }

        // 通知所有訂閱者 (例如 Arrow) 自我銷毀
        TriggerEpisodeEnd();

        if (playerAgent != null) playerAgent.EndEpisode();
        episodeTimer = 0f;
    }

    public void TriggerEpisodeEnd()
    {
        OnEpisodeEnd?.Invoke();
    }
}