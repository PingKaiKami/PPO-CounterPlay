using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;
using Unity.MLAgents.Policies;

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
    public AttackData[] attackPatterns; // 創建一個 AttackData 的陣列
    // public int damage = 1;
    // public float startUpTime = 0.5f;
    // public float recoveryTime = 1f;

    // --- ML-Agents & 遊戲邏輯相關變數 ---
    public Transform playerTransform;
    public float maxEpisodeTime = 60f; // 最大回合時間 
    private float episodeTimer;        // 當前回合的計時器
    private HP player_HP;
    private HP enemy_HP;

    private Material mat;
    private Color oriColor;
    private AttackRange_Enemy attackRange_enemy;
    private Player_Behavior player_Behavior;
    private Rigidbody rb;
    private Vector3 moveDirection;
    private bool isRandom = false;
    private float attackRange;
    private float playerAttackRange;

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
        int newAttackBranchSize = 1 + attackPatterns.Length;

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
            Debug.Log($"Attack Branch Size for {gameObject.name} was dynamically set to {newAttackBranchSize} based on {attackPatterns.Length} attack patterns.");
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
            playerAttackRange = playerTransform.GetComponentInChildren<AttackRange>().radius;
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

    // 收集觀測資訊 (Agent的"眼睛")
    public override void CollectObservations(VectorSensor sensor)
    {
        if (playerTransform == null || player_Behavior == null)
        {
            // 如果找不到玩家，提供 9 個 0 作為佔位符
            for (int i = 0; i < 9; i++) sensor.AddObservation(0f);
            return;
        }

        // 1. 自己到玩家的方向向量 (3 個值)
        Vector3 toPlayer = playerTransform.position - transform.position;
        sensor.AddObservation(toPlayer.normalized);

        // 2. 距離(1 個值)
        sensor.AddObservation(toPlayer.magnitude);

        // 3. 玩家速度向量 (3 個值)
        // 確保 rigidbody 不是 null
        Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
        sensor.AddObservation(playerRb != null ? playerRb.velocity : Vector3.zero);

        // 4. 玩家的詳細狀態 (PlayerState) (1 個值)
        // 將 enum 轉換為整數，讓 AI 學習
        sensor.AddObservation((int)player_Behavior.GetCurrentState());

        // 5. 自己的狀態 (EnemyState) (1 個值)
        sensor.AddObservation((int)curState);

        // 6. 自己是否在玩家的攻擊範圍內 (1 個值)
        // 讓 AI 知道它是否處於危險之中
        sensor.AddObservation(player_Behavior.IsEnemyInAttackRange(gameObject));
    }

    // 接收來自神經網路的動作指令
    public override void OnActionReceived(ActionBuffers actions)
    {
        // --- 1. 持續更新移動和朝向 ---
        // 無論處於何種狀態，AI都應該根據決策更新它想去哪裡。
        int moveAction = actions.DiscreteActions[0];
        UpdateMoveDirection(moveAction);

        // --- 2. 根據狀態決定是否能執行新指令 (攻擊/防禦) ---
        // 如果正在攻擊流程中或防禦中，則不處理新的攻擊或防禦指令。
        if (curState == EnemyState.StartUp || curState == EnemyState.Recovery || curState == EnemyState.Defending)
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
            if (attackIndex < attackPatterns.Length)
            {
                // 獲取對應的攻擊數據
                AttackData selectedAttack = attackPatterns[attackIndex];
                
                // 立即改變狀態以防止重複觸發
                curState = EnemyState.StartUp;
                
                // 啟動協程，並將選中的攻擊數據傳遞過去
                StartCoroutine(Attack(selectedAttack));
            }
        }

        Vector3 selfXZ = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 playerXZ = new Vector3(playerTransform.position.x, 0, playerTransform.position.z);

        float distanceToPlayer = Vector3.Distance(selfXZ, playerXZ);

        if (distanceToPlayer <= playerAttackRange)
        {
            AddReward(0.0002f);
        }
        else if (distanceToPlayer <= attackRange && distanceToPlayer > playerAttackRange)
        {
            AddReward(0.001f);
        }
        AddReward(-0.0001f);
    }

    // 新增一個輔助函式來處理移動方向的更新，讓 OnActionReceived 更乾淨
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
        // --- 1. 更新計時器 ---
        episodeTimer += Time.fixedDeltaTime;

        // --- 2. 檢查結束條件 ---
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

        // --- 3. 執行原有的移動邏輯 ---
        if (curState != EnemyState.Recovery && curState != EnemyState.Defending)
        {
            if (moveDirection != Vector3.zero)
            {
                Vector3 targetPos = rb.position + moveDirection.normalized * moveSpeed * Time.fixedDeltaTime;
                rb.MovePosition(targetPos);
            }
        }
    }

    IEnumerator Attack(AttackData attackData)
    {
        attackRange_enemy.ChangeRange(attackData);
        player_Behavior.ChangeDistanceToEnemy(attackData.attackRange);

        float time = 0f;
        curState = EnemyState.StartUp;
        Color targetColor = Color.red;
        while (time < attackData.startUpTime)
        {
            time += Time.deltaTime;
            float t = time / attackData.startUpTime;
            mat.color = Color.Lerp(oriColor, targetColor, t);
            yield return null;
        }

        mat.color = targetColor;

        bool isInRange = attackRange_enemy.IsInRange();
        if (isInRange)
        {
            if (player_HP != null)
            {
                if (player_Behavior.GetCurrentState() == Player_Behavior.PlayerState.Defending)
                {
                    player_HP.Hurt(attackData.damage / 10);
                }
                else
                {
                    player_HP.Hurt(attackData.damage);
                }
            }
        }
        else
        {
            AddReward(-0.2f);
        }

        curState = EnemyState.Recovery;
        mat.color = Color.green;
        yield return new WaitForSeconds(attackData.recoveryTime);

        mat.color = oriColor;
        curState = EnemyState.Idle;
    }

    // 用於手動測試，確保動作設定正確
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions.Clear();

        // 手動控制移動
        int moveAction = 0; // 0:不動
        if (Input.GetKey(KeyCode.UpArrow)) moveAction = 1;
        if (Input.GetKey(KeyCode.DownArrow)) moveAction = 2;
        if (Input.GetKey(KeyCode.LeftArrow)) moveAction = 3;
        if (Input.GetKey(KeyCode.RightArrow)) moveAction = 4;

        // 手動控制攻擊
        int attackAction = Input.GetKey(KeyCode.F1) ? 1 : 0;

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
}