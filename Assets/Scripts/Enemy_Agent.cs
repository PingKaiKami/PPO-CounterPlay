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
        if (playerTransform == null || player_Behavior == null)
        {
            for (int i = 0; i < 13; i++) sensor.AddObservation(0f);
            return;
        }

        // 1. 自己的面對方向 (3 個值)
        sensor.AddObservation(enemyObj.forward.normalized);

        // 2. 自己到玩家的方向向量 (3 個值)
        Vector3 toPlayer = playerTransform.position - transform.position;
        sensor.AddObservation(toPlayer.normalized);

        // 3. 自己是否正對著玩家 (1 個值)
        float alignment = Vector3.Dot(enemyObj.forward.normalized, toPlayer.normalized);
        sensor.AddObservation(alignment);

        // 4. 距離(1 個值)
        sensor.AddObservation(toPlayer.magnitude);

        // 5. 玩家速度向量 (3 個值)
        // 確保 rigidbody 不是 null
        Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
        sensor.AddObservation(playerRb != null ? playerRb.velocity : Vector3.zero);

        // 6. 玩家的詳細狀態 (PlayerState) (1 個值)
        // 將 enum 轉換為整數，讓 AI 學習
        sensor.AddObservation((int)player_Behavior.GetCurrentState());

        // 7. 自己的狀態 (EnemyState) (1 個值)
        sensor.AddObservation((int)curState);
    }

    // 接收來自神經網路的動作指令
    public override void OnActionReceived(ActionBuffers actions)
    {
        // --- 1. 持續更新移動和朝向 ---
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
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        float minDistance = 1;
        float maxDistance = attackRange;
        float bestDistance = Mathf.Max(attackRange - 0.5f, minDistance);
        float maxDistanceReward = 0.001f;

        float distanceScore;

        if (distanceToPlayer >= bestDistance)
        {
            distanceScore = Mathf.InverseLerp(maxDistance, bestDistance, distanceToPlayer);
        }
        else
        {
            distanceScore = Mathf.InverseLerp(minDistance, bestDistance, distanceToPlayer);
        }

        distanceScore = Mathf.Clamp01(distanceScore);

        float reward = distanceScore * maxDistanceReward;
        Debug.Log("distanceReward : " + reward);
        AddReward(reward);

        // 面對方向Reward
        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0;

        float alignment = Vector3.Dot(enemyObj.forward.normalized, toPlayer.normalized);

        if (alignment > 0)
        {
            float aimingReward = alignment * (1.0f / (1.0f + distanceToPlayer));
            Debug.Log("aimingReward : " + aimingReward * 0.005f);
            AddReward(aimingReward * 0.005f);
        }
        else
        {
            AddReward(-0.005f);
        }

        // 閃避長距離攻擊Reward
        List<GameObject> nearbyProjectiles = GetNearbyProjectiles(10f);
        if (nearbyProjectiles.Count > 0)
        {
            float dangerScore = 0f;

            Vector3 myIntendedVelocity = moveDirection.normalized * moveSpeed;
            // ===========================================

            foreach (var projectile in nearbyProjectiles)
            {
                Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
                if (projectileRb == null) continue;

                Vector3 projectileVel = projectileRb.velocity;
                Vector3 toMe = transform.position - projectile.transform.position;

                if (Vector3.Dot(projectileVel.normalized, toMe.normalized) > 0.9f)
                {
                    // 使用 myIntendedVelocity 來計算躲避得分
                    float escapeScore;

                    if (myIntendedVelocity.sqrMagnitude < 0.01f)
                    {
                        escapeScore = 0f;
                    }
                    else
                    {
                        float dotMyVelToProjectileVel = Vector3.Dot(myIntendedVelocity.normalized, projectileVel.normalized);
                        escapeScore = 1.0f - Mathf.Abs(dotMyVelToProjectileVel);
                    }
                    dangerScore += escapeScore * (1.0f / (1.0f + toMe.magnitude));
                }
            }
            if (dangerScore > 0)
            {
                Debug.Log("dangerScore : " + dangerScore * 0.1f);
                AddReward(dangerScore * 0.1f);
            }
            else
            {
                AddReward(-0.005f);
            }
        }

        AddReward(-0.0001f);
    }

    private List<GameObject> GetNearbyProjectiles(float radius)
    {
        List<GameObject> projectiles = new List<GameObject>();
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Projectile"))
            {
                projectiles.Add(hit.gameObject);
            }
        }
        return projectiles;
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
            AddReward(timeBonus * 2.0f);
            SetReward(1);
            Debug.Log($"Episode End: Player Died (Enemy Wins) Cumulative Reward: {GetCumulativeReward()}");
            EndEpisode();
            return;
        }

        // 條件 b: 敵人自己死亡 (敵人失敗)
        if (enemy_HP != null && enemy_HP.IsDead())
        {
            SetReward(-0.5f);
            Debug.Log($"Episode End: Enemy Died (Player Wins) Cumulative Reward: {GetCumulativeReward()}");
            EndEpisode();
            return;
        }

        // 條件 c: 時間到 (平手或未完成)
        if (episodeTimer >= maxEpisodeTime)
        {
            SetReward(-1);
            Debug.Log($"Episode End: Time Out. Cumulative Reward: {GetCumulativeReward()}");
            EndEpisode();
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

    IEnumerator Attack(ComboData combo)
    {
        Debug.Log($"Start Attack : {combo.comboName}");

        foreach (AttackData step in combo.attackSteps)
        {
            attackRange_enemy.UpdateAttackShape(step);
            player_Behavior.ChangeDistanceToEnemy(step.attackRange * 1.2f);
            attackRange = step.attackRange;

            float time = 0f;
            curState = EnemyState.StartUp;
            Vector3 startPosition = transform.position;
            Vector3 endPosition = startPosition + enemyObj.forward * step.forwardMovement;
            Color targetColor = Color.red;
            while (time < step.startupTime)
            {
                time += Time.deltaTime;
                float t = time / step.startupTime;
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
                        player_HP.Hurt(step.damage / 10);
                    }
                    else
                    {
                        player_HP.Hurt(step.damage);
                    }
                }
            }
            else
            {
                if (angle > step.attackAngle / 2)
                {
                    AddReward(-0.2f);
                }
                AddReward(-0.05f);
            }

            curState = EnemyState.Recovery;
            mat.color = Color.green;
            yield return new WaitForSeconds(step.recoveryTime);

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
            if (Input.GetKey(KeyCode.Alpha1))
            {
                attackAction = 1;
            }

            if (comboPatterns.Length > 1 && Input.GetKey(KeyCode.Alpha2))
            {
                attackAction = 2;
            }

            if (comboPatterns.Length > 2 && Input.GetKey(KeyCode.Alpha3))
            {
                attackAction = 3;
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
        AddReward(damageDealt / 100.0f);
    }
    public void OnDamageTaken(int damageTaken)
    {
        AddReward(-damageTaken / 200.0f);
    }
    public void OnPlayerAttackMissed()
    {
        AddReward(0.1f);
    }
    public void OnPlayerLongAttackMissed()
    {
        AddReward(0.5f);
    }
}