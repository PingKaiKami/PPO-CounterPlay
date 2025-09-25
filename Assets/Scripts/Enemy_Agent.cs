using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;
using Unity.MLAgents.Policies;
using System.Collections.Generic;
using Unity.MLAgents.Integrations.Match3;
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
    private bool isRandom = false;
    private float attackRange = 2f;
    private float actionProgress;

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
    }

    // 當一個訓練"回合"開始時被呼叫
    public override void OnEpisodeBegin()
    {
        StopAllCoroutines();

        if ((player_Behavior != null && player_Behavior.curMode == Player_Behavior.TrainingMode.Random) || isRandom)
        {
            isRandom = true;
            player_Behavior.SetRandomTrainingMode();
        }

        episodeTimer = 0f;
        Vector3 newPos;
        do
        {
            newPos = new Vector3(Random.Range(-5, 5), 0, Random.Range(-5, 5));
        } while (newPos == Vector3.zero);
        transform.localPosition = newPos;

        playerTransform.localPosition = new Vector3(0, 0.5f, 0);

        curState = EnemyState.Idle;
        mat.color = oriColor;
        if (player_HP != null) player_HP.ResetHealth();
        if (enemy_HP != null) enemy_HP.ResetHealth();

    }

    // 收集觀測資訊
    public override void CollectObservations(VectorSensor sensor)
    {
        // 自己的面對方向 (3 個值)
        sensor.AddObservation(enemyObj.forward.normalized);

        // 自己到玩家的方向向量 (3 個值)
        Vector3 toPlayer = playerTransform.position - transform.position;
        sensor.AddObservation(toPlayer.normalized);

        // 自己到玩家的距離(1 個值)
        sensor.AddObservation(toPlayer.magnitude);

        // 玩家速度向量 (3 個值)
        // 確保 rigidbody 不是 null
        Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
        sensor.AddObservation(playerRb != null ? playerRb.velocity : Vector3.zero);

        // 玩家面對方向 (3 個值)
        sensor.AddObservation(playerTransform.forward.normalized);

        // 玩家的詳細狀態 (PlayerState) (1 個值)
        // 將 enum 轉換為整數，讓 AI 學習
        sensor.AddObservation((int)player_Behavior.GetCurrentState());

        // 自己的狀態 (EnemyState) (1 個值)
        sensor.AddObservation((int)curState);

        // 玩家當前動作進度
        sensor.AddObservation(player_Behavior.GetActionProgress());

        // 敵人當前動作進度
        sensor.AddObservation(actionProgress);

        // 敵人血量百分比
        sensor.AddObservation((float)enemy_HP.GetCurrentHealth() / enemy_HP.maxHP);
        
        // 玩家血量百分比
        sensor.AddObservation((float)player_HP.GetCurrentHealth() / player_HP.maxHP);
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
        // 距離Reward
        // float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // float minDistance = 1;
        // float maxDistance = attackRange;
        // float bestDistance = Mathf.Max(attackRange - 0.5f, minDistance);
        // float maxDistanceReward = 0.001f;

        // float distanceScore;

        // if (distanceToPlayer >= bestDistance)
        // {
        //     distanceScore = Mathf.InverseLerp(maxDistance, bestDistance, distanceToPlayer);
        // }
        // else
        // {
        //     distanceScore = Mathf.InverseLerp(minDistance, bestDistance, distanceToPlayer);
        // }

        // distanceScore = Mathf.Clamp01(distanceScore);

        // float reward = distanceScore * maxDistanceReward;
        // if (printState)
        // {
        //     Debug.Log("distanceReward : " + reward);    
        // }

        // AddReward(reward);

        // 面對方向Reward
        // Vector3 toPlayer = playerTransform.position - transform.position;
        // toPlayer.y = 0;

        // float alignment = Vector3.Dot(enemyObj.forward.normalized, toPlayer.normalized);

        // if (alignment > 0)
        // {
        //     float aimingReward = alignment * (1.0f / (1.0f + distanceToPlayer));
        //     if (printState)
        //     {
        //         Debug.Log("aimingReward : " + aimingReward * 0.005f);    
        //     }
        //     AddReward(aimingReward * 0.005f);
        // }
        // else
        // {
        //     AddReward(-0.005f);
        // }

        // 閃避長距離攻擊Reward
        if (player_Behavior.IsAiming())
        {
            Vector3 playerPosition = playerTransform.position;
            Vector3 playerForward = playerTransform.forward;

            RaycastHit hit;
            float raycastDistance = 15f;

            if (Physics.Raycast(playerPosition, playerForward, out hit, raycastDistance, -1, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.CompareTag("Enemy"))
                {
                    float distanceToPlayer = Vector3.Distance(transform.position, playerPosition);
                    float penalty = -(1.0f / (1.0f + distanceToPlayer)) * (1.0f / (1.0f + distanceToPlayer)) * Time.deltaTime * player_Behavior.GetActionProgress();

                    AddReward(penalty);
                    penalty1Sum += penalty;
                }
            }
            else
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerPosition);
                float reward = 1.0f / (1 + distanceToPlayer) * 1.0f / (1 + distanceToPlayer) * Time.deltaTime * player_Behavior.GetActionProgress();

                AddReward(reward);
                reward1Sum += reward;
            }
            Debug.DrawRay(playerPosition, playerForward * raycastDistance, Color.yellow);
        }
        else
        {
            if (printReward)
            {
                Debug.Log($"Aiming_Avoid_Reward: {reward1Sum}");
                Debug.Log($"Aiming_Avoid_Penalty: {penalty1Sum}");
            }
            reward1Sum = 0;
            penalty1Sum = 0;
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

                if (alignment > 0.95f)
                {
                    float distanceToProjectile = Vector3.Distance(transform.position, projectilePosition);

                    float penalty = -(1.0f / (1.0f + Mathf.Pow(distanceToProjectile, 2))) * Time.deltaTime;

                    AddReward(penalty);
                    penalty2Sum += penalty; 
                }
                else
                {
                    float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                    float reward = 1.0f / (1.0f + Mathf.Pow(distanceToPlayer, 2)) * Time.deltaTime;

                    AddReward(reward);
                    reward2Sum += reward;
                }
            }
        }
        else
        {
            if (printReward)
            {
                Debug.Log($"Arrow_Avoid_Reward: {reward2Sum}");
                Debug.Log($"Arrow_Avoid_Penalty: {penalty2Sum}");
            }
            reward2Sum = 0;
            penalty2Sum = 0;
        }
        
        AddReward(0.0001f);
    }
    private float reward1Sum;
    private float penalty1Sum;
    private float reward2Sum;
    private float penalty2Sum;
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
            enemyObj.forward = Vector3.Slerp(enemyObj.forward, moveDirection.normalized, Time.deltaTime * rotationSpeed);
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
            AddReward(timeBonus * 0.5f);
            if (enemy_HP.CurrentHP == 100)
            {
                AddReward(1);
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
        GameEvents.TriggerEpisodeEnd();

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
            attackRange = step.attackRange;

            float time = 0f;
            actionProgress = 0f;
            curState = EnemyState.StartUp;
            Vector3 startPosition = transform.position;
            Vector3 endPosition = startPosition + enemyObj.forward * step.forwardMovement;
            Color targetColor = Color.red;
            while (time < step.startupTime)
            {
                time += Time.deltaTime;
                float t = time / step.startupTime;
                actionProgress = t;
                float movementProgress = step.movementCurve.Evaluate(t);
                rb.MovePosition(Vector3.Lerp(startPosition, endPosition, movementProgress));

                mat.color = Color.Lerp(oriColor, targetColor, t);
                yield return null;
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
                    AddReward(-0.1f);
                }
                AddReward(-0.1f);
            }

            curState = EnemyState.Recovery;
            time = 0f;
            actionProgress = 0f;
            mat.color = Color.green;
            targetColor = oriColor;

            while (time < step.recoveryTime)
            {
                time += Time.deltaTime;
                float t = time / step.recoveryTime;
                actionProgress = t;
                mat.color = Color.Lerp(Color.green, targetColor, t);
                yield return null;
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
        if (printReward)
        {
            Debug.Log($"AttackReward : {damageDealt / 100.0f}");
        }
        AddReward(damageDealt / 100.0f);
    }
    public void OnDamageTakenFromMelee(int damageTaken)
    {
        AddReward(-damageTaken / 500.0f);
    }
    public void OnDamageTakenFromRanged(int damageTaken)
    {
        if (printReward)
        {
            Debug.Log($"PlayerLongAttackReward : {-damageTaken / 100.0f}");
        }
        AddReward(-damageTaken / 100.0f);
    }
    public void OnPlayerAttackMissed()
    {
        // AddReward(0.1f);
    }
    public void OnPlayerLongAttackMissed()
    {
        if (printReward)
        {
            Debug.Log("PlayerLongAttackMissReward : 0.2");
        }
        AddReward(0.2f);
    }
}