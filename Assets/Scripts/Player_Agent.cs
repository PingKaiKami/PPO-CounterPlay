using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class Player_Agent : Agent
{
    private Player_Behavior player;
    private Player_Behavior.TrainingMode currentDemoMode;

    private HP playerHP;
    private HP enemyHP;
    private TrainingArea myArea;

    private float episodeTimer;
    public float maxEpisodeTime = 60f;
    public bool isVerify = true;
    public string modelName = "Player_GAIL"; 

    public override void Initialize()
    {
        player = GetComponent<Player_Behavior>();
        playerHP = GetComponent<HP>();
        myArea = GetComponentInParent<TrainingArea>();

        if (player.enemy != null)
        {
            enemyHP = player.enemy.GetComponent<HP>();
        }
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        
        currentDemoMode = Player_Behavior.TrainingMode.AtkDashRanged;

        player.ResetStateToIdle();

        if (myArea != null) myArea.TriggerEpisodeEnd();
        if (playerHP != null) playerHP.ResetHealth();
        if (enemyHP != null) enemyHP.ResetHealth();
    }

    void FixedUpdate()
    {
        episodeTimer += Time.fixedDeltaTime;

        // 1. 檢查自己死亡 (Player 輸了 -> Enemy Wins)
        if (playerHP != null && playerHP.IsDead())
        {         
            // 【新增】回報給 BenchmarkManager
            if (BenchmarkManager.Instance != null && isVerify)
            {
                // 注意：這裡 winner 是 "Enemy"，因為 Player 死了
                BenchmarkManager.Instance.RecordEpisode(modelName, "Enemy", 0);
            }

            EndEpisode();
            return;
        }

        // 2. 檢查敵人死亡 (Player 贏了 -> Player Wins)
        if (enemyHP != null && enemyHP.IsDead())
        {
            // 【新增】回報給 BenchmarkManager
            if (BenchmarkManager.Instance != null && isVerify)
            {
                // 注意：這裡 winner 是 "Player"，因為 Enemy 死了
                BenchmarkManager.Instance.RecordEpisode(modelName, "Player", 0);
            }

            EndEpisode();
            return;
        }

        // 3. 時間到 (Draw)
        if (episodeTimer >= maxEpisodeTime)
        {
            // 【新增】回報給 BenchmarkManager
            if (BenchmarkManager.Instance != null && isVerify)
            {
                BenchmarkManager.Instance.RecordEpisode(modelName, "Draw", 0);
            }

            EndEpisode();
            return;
        }

        // 4. 掉出地圖
        if (transform.localPosition.y < -5f)
        {
            EndEpisode();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. 敵我向量 (3)
        if (player.target != null)
        {
            sensor.AddObservation((player.target.position - transform.position).normalized);
            sensor.AddObservation(Vector3.Distance(transform.position, player.target.position));
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
        }

        // 2. 自身狀態 (State Enum 轉 One-hot 或數值)
        sensor.AddObservation((int)player.GetCurrentState());
        
        // 3. 瞄準狀態
        sensor.AddObservation(player.IsAiming());
    }

    // 執行 (Agent 的手)
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (player.IsUseInternalLogic) return;
        
        // 1. 解析移動 (Continuous)
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        Vector3 moveVec = new Vector3(moveX, 0, moveZ).normalized;
        Vector3 worldMoveVec = transform.TransformDirection(moveVec);

        // 2. 解析技能 (Discrete)
        int skill = actions.DiscreteActions[0];
        bool dash = (skill == 1);
        bool dashAtk = (skill == 2);
        bool atk = (skill == 3);
        bool longAtk = (skill == 4);

        // 3. 【關鍵修改】決定面向 (Look Direction)
        // 預設看移動方向
        Vector3 lookDir = worldMoveVec;

        // 如果有敵人，強制「鎖定敵人」(這樣才能實現側跳射擊、後退射擊)
        if (player.target != null)
        {
            Vector3 dirToTarget = player.target.position - transform.position;
            dirToTarget.y = 0;
            // 只有當距離大於極小值才更新，避免重疊時亂轉
            if (dirToTarget.sqrMagnitude > 0.01f)
            {
                lookDir = dirToTarget.normalized;
            }
        }

        // 4. 執行
        // moveDir = worldMoveVec (想往哪走)
        // lookDir = lookDir (看著敵人)
        player.ExecuteAgentAction(worldMoveVec, lookDir, dash, dashAtk, atk, longAtk);
    }

    // 錄製 (Agent 的老師)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // 1. 問 Player_Behavior：如果用 currentDemoMode，我該做什麼？
        var intent = player.GetLogicIntent(currentDemoMode);

        // 2. 轉換移動意圖 (World -> Local)
        var continuousActions = actionsOut.ContinuousActions;

        // 將世界座標的方向 (intent.moveDirection) 轉換為本地座標 (相對於角色面向)
        // 例如：意圖是往角色正右方走，這裡會變成 (1, 0, 0)
        Vector3 localMoveDir = transform.InverseTransformDirection(intent.moveDirection);

        continuousActions[0] = localMoveDir.x; // X 軸：左右 (Strafe)
        continuousActions[1] = localMoveDir.z; // Z 軸：前後 (Forward/Back)

        // 3. 轉換技能意圖 -> Action
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0;

        if (intent.wantsDash) discreteActions[0] = 1;       // 1: Dash
        else if (intent.wantsDashAttack) discreteActions[0] = 2; // 2: DashAtk
        else if (intent.wantsAttack) discreteActions[0] = 3;     // 3: Atk
        else if (intent.wantsLongAttack) discreteActions[0] = 4; // 4: LongAtk
    }
}