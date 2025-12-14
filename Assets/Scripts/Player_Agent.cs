using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class Player_Agent : Agent
{
    private Player_Behavior player;
    // 用來記錄這回合要模仿哪個模式
    private Player_Behavior.TrainingMode currentDemoMode; 

    public override void Initialize()
    {
        player = GetComponent<Player_Behavior>();
    }

    public override void OnEpisodeBegin()
    {
        // 每次回合開始，隨機切換一種模仿對象
        // 這樣你的 Demo 檔案就會包含兩種策略的數據
        currentDemoMode = Random.value > 0.5f ? 
            Player_Behavior.TrainingMode.DashAtkRanged : 
            Player_Behavior.TrainingMode.AtkDashRanged;

        // 重要：要讓 Player_Behavior 重置狀態
        player.ResetStateToIdle();
        
        // 確保 Player 本身的模式設為手動或無，以免原本的 Update 自動執行邏輯干擾 Agent
        // 我們假設你有一個 'AgentControlled' 模式，或者直接暫停原本 Update 的邏輯
        // player.curMode = Player_Behavior.TrainingMode.Manual; 
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

        if (intent.wantsAttack) discreteActions[0] = 1;
        else if (intent.wantsDash) discreteActions[0] = 2;
        else if (intent.wantsLongAttack) discreteActions[0] = 3;
    }
}