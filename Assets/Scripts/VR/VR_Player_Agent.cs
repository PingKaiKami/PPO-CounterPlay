using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.InputSystem;

public class VR_Player_Agent : Agent
{
    public float maxEpisodeTime = 60f;
    public bool isVerify = true;
    public bool printDebugObs = true;
    public bool printDebugCont = true;
    public string modelName = "Player_GAIL"; 
    private VR_Player_Behavior player;
    private VR_Player_Behavior.TrainingMode currentDemoMode;

    private HP playerHP;
    private HP enemyHP;
    private VR_TrainingArea myArea;
    private Rigidbody rb;
    private VR_Sword sword;
    private float episodeTimer;

    void Start()
    {
        player = GetComponent<VR_Player_Behavior>();
        playerHP = GetComponent<HP>();
        myArea = GetComponentInParent<VR_TrainingArea>();
        rb = GetComponent<Rigidbody>();
        sword = player.activateSword.GetComponent<VR_Sword>();

        if (player.enemy != null)
        {
            enemyHP = player.enemy.GetComponent<HP>();
        }
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;

        player.ResetStateToIdle();

        currentDemoMode = player.curMode;

        if (playerHP != null) playerHP.ResetHealth();
        if (enemyHP != null) enemyHP.ResetHealth();
    }

    void FixedUpdate()
    {
        // 1. 累加計時器
        episodeTimer += Time.fixedDeltaTime;

        // 2. 死亡判定 (一定要用 GetComponentInChildren 確保抓到血量腳本)
        if (playerHP != null && playerHP.IsDead())
        {
            HandleEnd("Enemy"); // 玩家死，敵人贏
            return;
        }

        if (enemyHP != null && enemyHP.IsDead())
        {
            HandleEnd("Player"); // 敵人死，玩家贏
            return;
        }

        // 3. 超時判定
        if (episodeTimer >= maxEpisodeTime)
        {
            HandleEnd("Draw");
            return;
        }

        // 4. 掉落判定 (改用世界座標 y，防止局部座標誤差)
        if (transform.localPosition.y < -2f) 
        {
            HandleEnd("Fall");
            return;
        }
    }

    private void HandleEnd(string result)
    {
        // 紀錄 Benchmark (如果是測試模式)
        if (isVerify && BenchmarkManager.Instance != null)
        {
            BenchmarkManager.Instance.RecordEpisode(modelName, result, 0);
        }

        // 通知環境清理箭矢
        if (myArea != null) myArea.TriggerEpisodeEnd();

        // 重置計時器並結束
        episodeTimer = 0f;
        EndEpisode();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if(player.activateSword == null || sword == null) return;
        // --- 1. 敵方資訊 (共 9 維) ---
        Vector3 toTarget = (player.target.position - transform.position).normalized;
        float dist = Vector3.Distance(transform.position, player.target.position);
        int enemyState = (int)player.enemy.GetComponent<VR_Enemy>().GetEnemyState();
        float enemyProg = player.enemy.GetComponent<VR_Enemy>().GetActionProgress();
        // 取得敵人的相對速度 (相對於玩家的朝向)
        Vector3 enemyRelVel = transform.InverseTransformDirection(player.enemy.GetComponent<Rigidbody>().velocity);

        sensor.AddObservation(toTarget);    // 3
        sensor.AddObservation(dist);        // 1
        sensor.AddObservation(enemyState);  // 1
        sensor.AddObservation(enemyProg);   // 1
        sensor.AddObservation(enemyRelVel); // 3

        // --- 2. 玩家自身資訊 (共 6 維) ---
        // 自己的速度向量 (相對座標)，這能讓 AI 理解 Dash 的物理慣性
        Vector3 myRelVel = transform.InverseTransformDirection(rb.velocity);
        Vector3 myForward = transform.forward;

        sensor.AddObservation(myRelVel);    // 3
        sensor.AddObservation(myForward);   // 3

        // --- 3. 武器物理與手部 (共 12 維) ---
        Vector3 relativeSwordTip = transform.InverseTransformPoint(sword.swordTip.position);
        Vector3 swordLPos = player.activateSword.transform.localPosition;
        Vector3 swordFwd = player.activateSword.transform.forward;
        Vector3 swordUp = player.activateSword.transform.up;

        sensor.AddObservation(relativeSwordTip); // 3
        sensor.AddObservation(swordLPos);        // 3
        sensor.AddObservation(swordFwd);         // 3
        sensor.AddObservation(swordUp);          // 3

        // ====== 總計 9 + 6 + 12 = 27 維 ======

        // --- Debug Print 部分 ---
        if (printDebugObs)
        {
            string enemyLog = $"<color=#FF4444>[ENEMY]</color> Dist:{dist:F2}, State:{enemyState}, RelVel:{enemyRelVel:F2}";
            string selfLog = $"<color=#44FF44>[SELF]</color> RelVel:{myRelVel:F2}, Fwd:{myForward:F2}";
            string swordLog = $"<color=#4444FF>[SWORD]</color> TipRel:{relativeSwordTip:F2}, SwordUp:{swordUp:F2}, SwordFwd:{swordFwd:F2}";

            Debug.Log($"{enemyLog} | {selfLog} | {swordLog}");
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (player.IsUseInternalLogic) return;
        
        // --- 1. 解析身體移動 (Continuous 0, 1) ---
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        Vector3 worldMoveVec = transform.TransformDirection(new Vector3(moveX, 0, moveZ));

        // --- 2. 解析武器位置 (Continuous 2, 3, 4) ---
        Vector3 swordTargetPos = new Vector3(actions.ContinuousActions[2], actions.ContinuousActions[3], actions.ContinuousActions[4]);
        swordTargetPos = Vector3.ClampMagnitude(swordTargetPos, 0.5f); // 這裡限制範圍

        // --- 3. 解析武器旋轉 (Continuous 5, 6, 7, 8) ---
        float xAngle = Mathf.Atan2(actions.ContinuousActions[5], actions.ContinuousActions[6]) * Mathf.Rad2Deg;
        float zAngle = Mathf.Atan2(actions.ContinuousActions[7], actions.ContinuousActions[8]) * Mathf.Rad2Deg;
        Quaternion swordTargetRot = Quaternion.Euler(xAngle, 0f, zAngle);

        // --- 4. 解析衝刺 (Discrete 0) ---
        bool dash = (actions.DiscreteActions[0] == 1);

        // --- 5. 決定看向方向 ---
        Vector3 lookDir = worldMoveVec;
        if (player.target != null) {
            Vector3 dir = player.target.position - transform.position;
            if (dir.sqrMagnitude > 0.01f) lookDir = dir.normalized;
        }

        // --- 6. 一鍵執行 ---
        player.ExecuteAgentAction(worldMoveVec, lookDir, dash, swordTargetPos, swordTargetRot);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;
        var disc = actionsOut.DiscreteActions;
        
        // 1. 取得基礎意圖 (移動與衝刺)
        var intent = player.GetLogicIntent(currentDemoMode);
        if (player.CurrentState == VR_Player_Behavior.PlayerState.Attacking)
        {
            cont[0] = 0f; // X 軸歸零
            cont[1] = 0f; // Z 軸歸零
        }
        else
        {
            Vector3 localMove = transform.InverseTransformDirection(intent.moveDirection);
            cont[0] = localMove.x;
            cont[1] = localMove.z;
        }
        disc[0] = intent.wantsDash ? 1 : 0;

        // 2. 直接讀取武器目前的物理資訊 (核心修改)
        if (player.activateSword != null)
        {
            Vector3 offsetPos = intent.swordTargetLocalPos;

            cont[2] = offsetPos.x;
            cont[3] = offsetPos.y;
            cont[4] = offsetPos.z;

            Vector3 currentEuler = intent.swordTargetLocalRot.eulerAngles;

            // X 軸旋轉的 Sin/Cos
            cont[5] = Mathf.Sin(currentEuler.x * Mathf.Deg2Rad);
            cont[6] = Mathf.Cos(currentEuler.x * Mathf.Deg2Rad);

            // Z 軸旋轉的 Sin/Cos
            cont[7] = Mathf.Sin(currentEuler.z * Mathf.Deg2Rad);
            cont[8] = Mathf.Cos(currentEuler.z * Mathf.Deg2Rad);
        }
        else
        {
            // 萬一沒武器，全部歸零
            for (int i = 2; i <= 8; i++) cont[i] = 0f;
        }

        if (printDebugCont)
        {
            float moveX = cont[0];
            float moveZ = cont[1];

            float swordX = cont[2];
            float swordY = cont[3];
            float swordZ = cont[4];

            // 計算解碼後的手動角度 (方便人類閱讀)
            float debugXAngle = Mathf.Atan2(cont[5], cont[6]) * Mathf.Rad2Deg;
            float debugZAngle = Mathf.Atan2(cont[7], cont[8]) * Mathf.Rad2Deg;

            // 取得衝刺狀態
            int dashValue = disc[0];

            // 格式化輸出
            string actionLog = $"<color=orange>[ACTION]</color> " +
                $"<b>Move:</b>({moveX:F2}, {moveZ:F2}) | " +
                $"<b>SwordPos:</b>({swordX:F2}, {swordY:F2}, {swordZ:F2}) | " +
                $"<b>SwordRot:</b>(X:{debugXAngle:F0}°, Z:{debugZAngle:F0}°) | " +
                $"<b>Dash:</b>{(dashValue == 1 ? "<color=red>ON</color>" : "OFF")}";

            Debug.Log(actionLog);
        }
    }
}
