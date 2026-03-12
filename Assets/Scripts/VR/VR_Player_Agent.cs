using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class VR_Player_Agent : Agent
{
    private VR_Player_Behavior player;
    private VR_Player_Behavior.TrainingMode currentDemoMode;

    private HP playerHP;
    private HP enemyHP;
    private VR_TrainingArea myArea;

    private Rigidbody rb;

    private float episodeTimer;
    public float maxEpisodeTime = 60f;
    public bool isVerify = true;
    public bool printDebugObs = true;
    public string modelName = "Player_GAIL"; 

    public override void Initialize()
    {
        player = GetComponent<VR_Player_Behavior>();
        playerHP = GetComponent<HP>();
        myArea = GetComponentInParent<VR_TrainingArea>();
        rb = GetComponent<Rigidbody>();

        if (player.enemy != null)
        {
            enemyHP = player.enemy.GetComponent<HP>();
        }
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        
        currentDemoMode = player.curMode;

        player.ResetStateToIdle();

        if (myArea != null) myArea.TriggerEpisodeEnd();
        if (playerHP != null) playerHP.ResetHealth();
        if (enemyHP != null) enemyHP.ResetHealth();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toTarget = Vector3.zero;
        float dist = 0f;
        if (player.target != null)
        {
            toTarget = (player.target.position - transform.position).normalized;
            dist = Vector3.Distance(transform.position, player.target.position);
        }
        
        int selfState = (int)player.GetCurrentState();
        float actionProgress_player = player.GetActionProgress();
        
        int enemyState = 0;
        if (player.enemy != null) enemyState = (int)player.enemy.GetComponent<VR_Enemy>().GetEnemyState();
        float actionProgress_enemy = player.enemy.GetComponent<VR_Enemy>().GetActionProgress();

        Vector3 myForward = transform.forward;
        
        Vector3 myVel = Vector3.zero;
        if (player.GetComponent<Rigidbody>() != null) myVel = player.GetComponent<Rigidbody>().velocity;
        
        sensor.AddObservation(toTarget); // 3
        sensor.AddObservation(dist); // 1
        sensor.AddObservation(selfState); // 1
        sensor.AddObservation(enemyState); // 1
        sensor.AddObservation(actionProgress_player); // 1
        sensor.AddObservation(actionProgress_enemy); // 1
        
        // sensor.AddObservation(myForward);
        // sensor.AddObservation(myVel);

        if (printDebugObs)
        {
            string log = $"[OBS] Dist: {dist:F2} | " +
                        // 顯示自身狀態與進度 (用 F2 簡化小數點)
                        $"Self: St{selfState}/Prog{actionProgress_player:F2} | " +
                        // 顯示敵人狀態與進度
                        $"Enemy: St{enemyState}/Prog{actionProgress_enemy:F2} | ";
            
            Debug.Log(log);
        }
    }

    // 執行 (Agent 的手)
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (player.IsUseInternalLogic) return;

        var behaviorType = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorType;
        
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

        // 3. 轉換技能意圖 -> Action
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0;

        if (intent.wantsDash) discreteActions[0] = 1;       // 1: Dash
        else if (intent.wantsDashAttack) discreteActions[0] = 2; // 2: DashAtk
        else if (intent.wantsAttack) discreteActions[0] = 3;     // 3: Atk
        else if (intent.wantsLongAttack) discreteActions[0] = 4; // 4: LongAtk
        else // 不攻擊才能移動
        {
            Vector3 localMoveDir = transform.InverseTransformDirection(intent.moveDirection);

            continuousActions[0] = localMoveDir.x; // X 軸：左右 (Strafe)
            continuousActions[1] = localMoveDir.z; // Z 軸：前後 (Forward/Back)
        }
    }
}