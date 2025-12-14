using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;
using Unity.MLAgents.Policies;
using System.Collections.Generic;
public class Enemy_Agent : Agent
{
    public enum EnemyState
    {
        Idle,
        StartUp,
        Recovery,
        Defending
    }
    public EnemyState curState = EnemyState.Idle;
    public Transform orientation;
    public Transform enemyObj;
    public float moveSpeed = 3f;
    public float rotationSpeed = 3f;

    [Header("Attack Patterns")]
    public ComboData[] comboPatterns;

    // --- ML-Agents & 遊戲邏輯相關變數 ---
    public Transform playerTransform;
    public float maxEpisodeTime = 60f;

    [Header("Debug")]
    public bool printReward = false;
    public bool printEndReward = true;
    public bool printState = false;
    private float episodeTimer;
    private HP player_HP;
    private HP enemy_HP;

    private Material mat;
    private Color oriColor;
    private AttackRange_Enemy attackRange_enemy;
    private Player_Behavior player_Behavior;
    private Rigidbody rb;
    private Vector3 moveDirection;
    private float actionProgress;
    private TrainingArea myArea;

    void Awake()
    {
        // ================== 動態調整 Branch Size 的核心邏輯 ==================

        // 1. 獲取掛載在同一個物件上的 BehaviorParameters 元件
        var behaviorParameters = GetComponent<BehaviorParameters>();


        // 2. 獲取當前的動作規範 (ActionSpec)
        //    ActionSpec 是一個包含了所有分支資訊的結構
        ActionSpec actionSpec = behaviorParameters.BrainParameters.ActionSpec;

        // 3. 計算新的攻擊分支大小
        //    大小 = 1 (不攻擊) + 攻擊模式的數量
        int newAttackBranchSize = 1 + comboPatterns.Length;

        // 4. 檢查當前設定是否已經是正確的
        if (actionSpec.BranchSizes[1] != newAttackBranchSize)
        {
            // 如果不正確，則創建一個新的 BranchSizes 陣列
            // 先複製舊的設定
            int[] newBranchSizes = (int[])actionSpec.BranchSizes.Clone();

            // 修改指定分支的大小
            newBranchSizes[1] = newAttackBranchSize;

            // 5. 將修改後的新設定應用回去
            actionSpec.BranchSizes = newBranchSizes;

            // 更新 BehaviorParameters 的 BrainParameters
            // 這一行在新版的 ML-Agents 中可能不是必須的，但加上更保險
            behaviorParameters.BrainParameters.ActionSpec = actionSpec;

            // 打印一條日誌，讓你知道程式碼已經自動修改了設定
            Debug.Log($"Attack Branch Size for {gameObject.name} was dynamically set to {newAttackBranchSize} based on {comboPatterns.Length} attack patterns.");
        }
        // =======================================================================
    }
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        attackRange_enemy = GetComponentInChildren<AttackRange_Enemy>();
        mat = GetComponentInChildren<Renderer>().material;
        oriColor = mat.color;

        if (playerTransform != null)
        {
            player_HP = playerTransform.GetComponent<HP>();
            player_Behavior = playerTransform.GetComponent<Player_Behavior>();
        }
        enemy_HP = GetComponent<HP>();
        myArea = GetComponentInParent<TrainingArea>();
    }

    // 當一個訓練"回合"開始時被呼叫
    public override void OnEpisodeBegin()
    {
        StopAllCoroutines();

        episodeTimer = 0f;
        Vector3 newPos;
        do
        {
            newPos = new Vector3(Random.Range(-5, 5), 0, Random.Range(-5, 5));
        } while (newPos == Vector3.zero);
        transform.localPosition = newPos;

        curState = EnemyState.Idle;
        mat.color = oriColor;
        if (enemy_HP != null) enemy_HP.ResetHealth();
        if (player_HP != null) player_HP.ResetHealth();
        
    }

    // 收集觀測資訊
    public override void CollectObservations(VectorSensor sensor)
    {
        List<float> observations = GetCurrentObservationsAsList();
        // Debug.Log($"[Obs] Count: {observations.Count}, Data: {string.Join(", ", observations.ConvertAll(f => f.ToString("F2")))}");
        
        foreach (float val in observations)
        {
            sensor.AddObservation(val);
        }
    }

    private List<float> GetCurrentObservationsAsList()
    {
        List<float> obs = new List<float>();
        Vector3 toPlayer = playerTransform.position - transform.position;
        Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();

        Vector3 enemyForward = enemyObj.forward.normalized;
        obs.AddRange(new float[] { enemyForward.x, enemyForward.y, enemyForward.z });
        Vector3 toPlayerDir = toPlayer.normalized;
        obs.AddRange(new float[] { toPlayerDir.x, toPlayerDir.y, toPlayerDir.z });
        obs.Add(toPlayer.magnitude);
        Vector3 playerVel = playerRb != null ? playerRb.velocity : Vector3.zero;
        obs.AddRange(new float[] { playerVel.x, playerVel.y, playerVel.z });
        Vector3 playerForward = playerTransform.forward.normalized;
        obs.AddRange(new float[] { playerForward.x, playerForward.y, playerForward.z });
        obs.Add((int)player_Behavior.GetCurrentState());
        obs.Add((int)curState);
        obs.Add(player_Behavior.GetActionProgress());
        obs.Add(actionProgress);
        obs.Add((float)enemy_HP.GetCurrentHealth() / enemy_HP.maxHP);
        obs.Add((float)player_HP.GetCurrentHealth() / player_HP.maxHP);

        return obs;
    }

    // 在 Enemy_Agent.cs 中添加以下方法

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        // actionMask 的用法：
        // actionMask.SetActionEnabled(branchIndex, actionIndex, isEnabled);
        
        // --- 分支 0: 移動 (0:不動, 1:前, 2:後, 3:左, 4:右) ---
        // --- 分支 1: 攻擊 (0:不攻, 1:普攻, 2:連擊, 3:重擊, 4:快攻) ---

        // 規則 1: 如果正在攻擊 (StartUp) 或恢復 (Recovery) 或 防禦 (Defending)
        //         則禁止所有「主動動作」(移動和發起新攻擊)
        //         只允許「什麼都不做」(Index 0)
        
        if (curState == EnemyState.StartUp || curState == EnemyState.Recovery || curState == EnemyState.Defending)
        {
            // (攻擊): 禁用 1~4，只留 0 (不攻)
            actionMask.SetActionEnabled(1, 1, false);
            actionMask.SetActionEnabled(1, 2, false);
            actionMask.SetActionEnabled(1, 3, false);
            actionMask.SetActionEnabled(1, 4, false);
        }
        else
        {
            // 規則 2 (可選): 根據距離或其他條件禁用特定攻擊
            // 這裡可以根據需要添加，例如如果沒體力就不能重擊等
            // 目前先保持預設 (全部啟用)
        }
    }

    // 接收來自神經網路的動作指令 (1 個值)
    public override void OnActionReceived(ActionBuffers actions)
    {
        int moveAction = actions.DiscreteActions[0];
        UpdateMoveDirection(moveAction);
        // --- 2. 根據狀態決定是否能執行新指令 (攻擊/防禦) ---
        // 如果正在攻擊流程中或防禦中，則不處理新的攻擊或防禦指令。
        if (curState == EnemyState.StartUp || curState == EnemyState.Defending)
        {
            // 如果從防禦狀態釋放，則變回Idle
            // if (curState == EnemyState.Defending && actions.DiscreteActions[2] == 0)
            // {
            //     curState = EnemyState.Idle;
            //     mat.color = oriColor;
            // }
            // 分支 0 (移動): 禁用 1~4，只留 0 (不動)
            return;
        }
        // --- 3. 執行狀態切換指令 (此時狀態必為 Idle) ---
        int attackAction = actions.DiscreteActions[1];
        // int defendAction = actions.DiscreteActions[2];

        // if (defendAction == 1)
        // {
        //     curState = EnemyState.Defending;
        //     mat.color = Color.blue;
        // }
        if (attackAction > 0 && curState == EnemyState.Idle)
        {
            // 動作值從 1 開始，對應陣列索引從 0 開始，所以需要 -1
            int attackIndex = attackAction - 1;

            // 安全檢查，確保索引不會超出陣列範圍
            if (attackIndex < comboPatterns.Length)
            {
                // 獲取對應的攻擊數據
                ComboData selectedAttack = comboPatterns[attackIndex];

                // 立即改變狀態以防止重複觸發
                curState = EnemyState.StartUp;

                // 啟動協程，並將選中的攻擊數據傳遞過去
                StartCoroutine(Attack(selectedAttack));
            }
        }
        

        // 閃避長距離攻擊Reward
        if (player_Behavior.IsAiming())
        {
            Vector3 playerPosition = playerTransform.position;
            Vector3 playerForward = playerTransform.forward;

            Vector3 directionToMe = (transform.position - playerPosition).normalized;

            float alignment = Vector3.Dot(playerForward, directionToMe);

            if (alignment > 0.95f)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerPosition);
                float penalty = -(1.0f / (1.0f + Mathf.Pow(distanceToPlayer, 2))) * player_Behavior.GetActionProgress() * 0.02f;

                AddCustomReward(penalty, "long_attack_penalty");
            }
            else
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerPosition);
                float reward = 1.0f / (1.0f + Mathf.Pow(distanceToPlayer, 2)) * player_Behavior.GetActionProgress() * 0.02f;

                AddCustomReward(reward, "long_attack_reward");
            }
        }
        // 躲避箭矢Reward
        IReadOnlyList<GameObject> activeProjectiles = player_Behavior.ActiveProjectiles;
        if (activeProjectiles.Count > 0)
        {
            foreach (var projectile in activeProjectiles)
            {
                if (projectile == null) continue;

                Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
                if (projectileRb == null || projectileRb.velocity.sqrMagnitude < 0.1f) continue;

                Vector3 projectilePosition = projectile.transform.position;
                Vector3 projectileDirection = projectileRb.velocity.normalized;

                Vector3 directionToMe = (transform.position - projectilePosition).normalized;

                float alignment = Vector3.Dot(projectileDirection, directionToMe);

                if (alignment > 0.99f)
                {
                    float distanceToProjectile = Vector3.Distance(transform.position, projectilePosition);

                    float penalty = -(1.0f / (1.0f + Mathf.Pow(distanceToProjectile, 2))) * 0.02f;

                    AddCustomReward(penalty, "avoid_arrow_panalty");
                }
                else
                {
                    float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                    float reward = 1.0f / (1.0f + Mathf.Pow(distanceToPlayer, 2)) * 0.02f;

                    AddCustomReward(reward, "avoid_arrow_reward");
                }
            }
        }

        AddCustomReward(-0.0001f, "constant_reward");
    }
    private void UpdateMoveDirection(int moveAction)
    {
        Vector3 moveInput = Vector3.zero;
        switch (moveAction)
        {
            case 1: moveInput = orientation.forward; break;
            case 2: moveInput = -orientation.forward; break;
            case 3: moveInput = -orientation.right; break;
            case 4: moveInput = orientation.right; break;
        }
        moveDirection = moveInput;
        moveDirection.y = 0f;

        if (moveDirection != Vector3.zero)
        {
            enemyObj.forward = Vector3.Slerp(enemyObj.forward, moveDirection.normalized, Time.fixedDeltaTime * rotationSpeed);
        }
    }

    // 在 FixedUpdate 中檢查結束條件是個好習慣，因為它與物理更新同步
    void FixedUpdate()
    {
        episodeTimer += Time.fixedDeltaTime;

        // 條件 a: 玩家死亡 (敵人獲勝)
        if (player_HP != null && player_HP.IsDead())
        {
            float timeBonus = (maxEpisodeTime - episodeTimer) / maxEpisodeTime;
            AddCustomReward(timeBonus * 0.5f, "timeBonus_reward");
            if (enemy_HP.CurrentHP == 100)
            {
                AddCustomReward(1, "fuul_health_reward");
            }
            SetReward(1);
            if (printEndReward)
            {
                Debug.Log($"Episode End: Player Died (Enemy Wins) Cumulative Reward: {GetCumulativeReward()}");
            }
            EndAndCleanupEpisode();
            return;
        }

        // 條件 b: 敵人自己死亡 (敵人失敗)
        if (enemy_HP != null && enemy_HP.IsDead())
        {
            SetReward(-1);
            if (printEndReward)
            {
                Debug.Log($"Episode End: Enemy Died (Player Wins) Cumulative Reward: {GetCumulativeReward()}");    
            }
            EndAndCleanupEpisode();
            return;
        }

        // 條件 c: 時間到 (平手或未完成)
        if (episodeTimer >= maxEpisodeTime)
        {
            SetReward(-2);
            if (printEndReward)
            {
                Debug.Log($"Episode End: Time Out. Cumulative Reward: {GetCumulativeReward()}");    
            }
            EndAndCleanupEpisode();
            return;
        }

        if (playerTransform.position.y < -5)
        {
            SetReward(0f);
            if (printEndReward)
            {
                Debug.Log($"Episode End: Player fall out of the map. Cumulative Reward: {GetCumulativeReward()}");    
            }
            EndAndCleanupEpisode();
            return;
        }

        if (curState != EnemyState.StartUp && curState != EnemyState.Defending)
        {
            if (moveDirection != Vector3.zero)
            {
                Vector3 targetPos = rb.position + moveDirection.normalized * moveSpeed * Time.fixedDeltaTime;
                rb.MovePosition(targetPos);
            }
        }
    }
    private void EndAndCleanupEpisode()
    {
        if (myArea != null)
        {
            myArea.TriggerEpisodeEnd();
        }
        EndEpisode();
    }

    IEnumerator Attack(ComboData combo)
    {
        if (printState)
        {
            Debug.Log($"Start Attack : {combo.comboName}");    
        }

        foreach (AttackData step in combo.attackSteps)
        {
            attackRange_enemy.UpdateAttackShape(step);
            // player_Behavior.ChangeDistanceToEnemy(step.attackRange * 1.2f);

            float time = 0f;
            actionProgress = 0f;
            curState = EnemyState.StartUp;
            Vector3 startPosition = transform.position;
            Vector3 endPosition = startPosition + enemyObj.forward * step.forwardMovement;
            Color targetColor = Color.red;
            while (time < step.startupTime)
            {
                time += Time.fixedDeltaTime;
                float t = time / step.startupTime;
                actionProgress = t;
                float movementProgress = step.movementCurve.Evaluate(t);
                rb.MovePosition(Vector3.Lerp(startPosition, endPosition, movementProgress));

                mat.color = Color.Lerp(oriColor, targetColor, t);
                yield return new WaitForFixedUpdate();
            }

            Vector3 directionToPlayer = playerTransform.position - transform.position;
            directionToPlayer.y = 0;
            Vector3 enemyForward = enemyObj.forward;
            enemyForward.y = 0;
            float angle = Vector3.Angle(enemyForward.normalized, directionToPlayer.normalized);

            mat.color = targetColor;

            bool isInRange = attackRange_enemy.IsPlayerInRange();
            if (isInRange)
            {
                if (player_HP != null)
                {
                    if (player_Behavior.GetCurrentState() == Player_Behavior.PlayerState.Defending)
                    {
                        player_HP.HurtFromMelee(step.damage / 10);
                    }
                    else
                    {
                        player_HP.HurtFromMelee(step.damage);
                    }
                }
            }
            else
            {
                if (angle > step.attackAngle / 2)
                {
                    AddCustomReward(-0.1f, "attack_angle_penalty");
                }
                AddCustomReward(-0.1f, "attack_failed_penalty");
            }

            curState = EnemyState.Recovery;
            time = 0f;
            actionProgress = 0f;
            mat.color = Color.green;
            targetColor = oriColor;

            while (time < step.recoveryTime)
            {
                time += Time.fixedDeltaTime;
                float t = time / step.recoveryTime;
                actionProgress = t;
                mat.color = Color.Lerp(Color.green, targetColor, t);
                yield return new WaitForFixedUpdate();
            }

            actionProgress = 0f;
            mat.color = oriColor;
            curState = EnemyState.Idle;
        }

    }

    // 用於手動測試，確保動作設定正確
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions.Clear();

        // 手動控制移動
        int moveAction = 0;
        if (Input.GetKey(KeyCode.UpArrow)) moveAction = 1;
        if (Input.GetKey(KeyCode.DownArrow)) moveAction = 2;
        if (Input.GetKey(KeyCode.LeftArrow)) moveAction = 3;
        if (Input.GetKey(KeyCode.RightArrow)) moveAction = 4;

        // 手動控制攻擊
        int attackAction = 0;
        if (comboPatterns != null && comboPatterns.Length > 0)
        {
            // normal attack
            if (Input.GetKey(KeyCode.Alpha1))
            {
                attackAction = 1;
            }
            // triple attack
            if (comboPatterns.Length > 1 && Input.GetKey(KeyCode.Alpha2))
            {
                attackAction = 2;
            }
            // strong attack
            if (comboPatterns.Length > 2 && Input.GetKey(KeyCode.Alpha3))
            {
                attackAction = 3;
            }
            // fast attack
            if (comboPatterns.Length > 3 && Input.GetKey(KeyCode.Alpha4))
            {
                attackAction = 4;
            }
        }

        // 手動控制防禦
        // int defendAction = Input.GetKey(KeyCode.F2) ? 1 : 0;

        discreteActions[0] = moveAction;
        discreteActions[1] = attackAction;
        // discreteActions[2] = defendAction;
    }
    public EnemyState GetEnemyState()
    {
        return curState;
    }
    public void OnDamageDealt(int damageDealt)
    {
        // float reward = damageDealt / 10.0f;
        // var pState = player_Behavior.GetCurrentState();

        // if (pState == Player_Behavior.PlayerState.Recovery)
        // {
        //     AddReward(2 * reward);
        // }
        // else
        // {
        //     AddReward(reward);
        // }
        AddCustomReward(damageDealt / 100.0f, "damage_reward");
    }
    public void OnDamageTakenFromMelee(int damageTaken)
    {
        AddCustomReward(-damageTaken / 50.0f, "damage_from_melee_penalty");
    }
    public void OnDamageTakenFromRanged(int damageTaken)
    {
        AddCustomReward(-damageTaken / 50.0f, "damage_from_ranged_penalty");
    }
    public void OnPlayerAttackMissed()
    {
        AddCustomReward(0.1f, "avoid_attack_reward");
    }
    public void OnPlayerLongAttackMissed()
    {
        // AddReward(0.2f);
    }

    private float currentStepReward = 0f;

    private void AddCustomReward(float value, string reason = "")
    {
        currentStepReward += value;  // 暫存總和
        AddReward(value);            // 繼續讓 ML-Agent 知道
        if (printReward && !string.IsNullOrEmpty(reason))
        {
            Debug.Log($"[Reward] {reason}: {value}");
        }
    }



    private void OnDrawGizmos()
    {
        if (player_Behavior == null || !Application.isPlaying) return;

        if (player_Behavior.IsAiming())
        {
            // --- 1. 定义锥形的参数 ---
            float alignmentThreshold = 0.95f; // 你的阈值
            float coneLength = 10f; // 锥形的长度，可以设得远一些

            // --- 2. 将点积阈值转换为角度 ---
            // a. alignment = cos(angle)，所以 angle = acos(alignment)
            // b. Mathf.Acos 返回的是弧度，需要乘以 Mathf.Rad2Deg 转换为角度
            // c. 这是“半角”，即中心线与锥形边缘的夹角
            float coneHalfAngle = Mathf.Acos(alignmentThreshold) * Mathf.Rad2Deg;

            // --- 3. 获取玩家的位置和朝向 ---
            Vector3 playerPosition = playerTransform.position;
            Vector3 playerForward = playerTransform.forward;
            
            // --- 4. 绘制“惩罚区”（内部的窄锥形） ---
#if UNITY_EDITOR
            // a. 设置颜色：半透明的红色，代表危险
            UnityEditor.Handles.color = new Color(1, 0, 0, 0.2f);

            // b. 计算锥形的一条起始边
            Quaternion leftRayRotation = Quaternion.AngleAxis(-coneHalfAngle, Vector3.up);
            Vector3 leftRayDirection = leftRayRotation * playerForward;
            
            // c. 使用 Handles.DrawSolidArc 绘制一个实心的扇形
            //    参数：中心点，法线，起始方向，总角度，半径
            UnityEditor.Handles.DrawSolidArc(playerPosition, Vector3.up, leftRayDirection, coneHalfAngle * 2, coneLength);
#endif

            // --- 5. (可选) 绘制“奖励区” ---
            // 我们可以绘制一个更大的、表示“安全区”的扇形
            // 例如，当 alignment < 0.99 时都是奖励区
            // 我们可以绘制一个从 +/- coneHalfAngle 到 +/- 90 度的区域
#if UNITY_EDITOR
            // a. 设置颜色：半透明的绿色，代表安全/奖励
            UnityEditor.Handles.color = new Color(0, 1, 0, 0.05f); // 更淡的颜色

            // b. 绘制左侧的安全区
            Quaternion farLeftRayRotation = Quaternion.AngleAxis(-90, Vector3.up);
            Vector3 farLeftRayDirection = farLeftRayRotation * playerForward;
            UnityEditor.Handles.DrawSolidArc(playerPosition, Vector3.up, farLeftRayDirection, 90 - coneHalfAngle, coneLength);

            // c. 绘制右侧的安全区
            // DrawSolidArc 需要起始方向，所以我们从右侧的起始边开始画
            Quaternion rightRayRotation = Quaternion.AngleAxis(coneHalfAngle, Vector3.up);
            Vector3 rightRayDirection = rightRayRotation * playerForward;
            // ============================================

            // 绘制从 +coneHalfAngle 度到 +90 度的区域
            UnityEditor.Handles.DrawSolidArc(playerPosition, Vector3.up, rightRayDirection, 90 - coneHalfAngle, coneLength);
#endif
        }

        IReadOnlyList<GameObject> activeProjectiles = player_Behavior.ActiveProjectiles;

        if (activeProjectiles.Count > 0)
        {
            float alignmentThreshold = 0.99f;
            float coneAngle = Mathf.Acos(alignmentThreshold) * Mathf.Rad2Deg;
            float coneLength = 15f;

            foreach (var projectile in activeProjectiles)
            {
                if (projectile == null) continue;
                Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
                
                Vector3 projectilePosition = projectile.transform.position;
                Vector3 projectileDirection = projectileRb.velocity.normalized;

                // --- 绘制水平扇形 ---
                Gizmos.color = Color.red;
                
                // 计算扇形的左右两条边
                Quaternion leftRayRotation = Quaternion.AngleAxis(-coneAngle, Vector3.up);
                Quaternion rightRayRotation = Quaternion.AngleAxis(coneAngle, Vector3.up);
                Vector3 leftRayDirection = leftRayRotation * projectileDirection;
                Vector3 rightRayDirection = rightRayRotation * projectileDirection;
                
                // 绘制边
                Gizmos.DrawRay(projectilePosition, leftRayDirection * coneLength);
                Gizmos.DrawRay(projectilePosition, rightRayDirection * coneLength);
                
                // 绘制中心线
                Gizmos.DrawRay(projectilePosition, projectileDirection * coneLength);

    #if UNITY_EDITOR
                // 绘制半透明的扇形填充
                UnityEditor.Handles.color = new Color(1, 0, 0, 0.1f);
                UnityEditor.Handles.DrawSolidArc(projectilePosition, Vector3.up, leftRayDirection, coneAngle * 2, coneLength);
    #endif
            }
        }
    }
}