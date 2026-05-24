using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine.InputSystem;

public class VR_EnemyDemo : Agent
{
    public int health;
    public Transform playerCamera;
    public XRBaseInteractor interactor_right;
    public float WeaponAngularVelocity { get; private set; }
    public Vector3 WeaponVelocity { get; private set; }
    public Vector3 CameraRotationAngles { get; private set; }
    public bool IsHoldingWeapon { get; private set; }

    private Vector3 lastPos;
    private Quaternion lastRot;
    private Transform currentWeaponTransform;
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
    public VR_SwordDemo sword;
    public MeshRenderer mr;
    public Animator animator;
    public float moveSpeed = 3f;
    public float rotationSpeed = 3f;

    [Header("Attack Patterns")]
    public ComboData[] comboPatterns;

    // --- ML-Agents & 遊戲邏輯相關變數 ---
    public Transform playerTransform;
    public float maxEpisodeTime = 60f;
    private VR_EnemyWeapon enemyWeapon;

    [Header("Debug")]
    public bool printReward = false;
    public bool printEndReward = true;
    public bool printState = false;

    [Header("Verify")]
    public bool isVerify = true;
    public string modelName;
    private float episodeTimer;
    private VR_HP_Player player_HP;
    private VR_HP_Enemy enemy_HP;

    private Material mat;
    private Color oriColor;
    private AttackRange_Enemy attackRange_enemy;
    private VR_Player_Behavior player_Behavior;
    private Rigidbody rb;
    private Vector3 moveDirection;
    private float actionProgress;
    public float GetActionProgress() => actionProgress;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        attackRange_enemy = GetComponentInChildren<AttackRange_Enemy>();
        mat = GetComponentInChildren<Renderer>().material;
        oriColor = mat.color;

        if (playerTransform != null)
        {
            player_HP = playerTransform.GetComponent<VR_HP_Player>();
            player_Behavior = playerTransform.GetComponent<VR_Player_Behavior>();
        }
        enemy_HP = GetComponent<VR_HP_Enemy>();
        if (interactor_right != null && interactor_right.hasSelection)
        {
            lastPos = interactor_right.interactablesSelected[0].transform.position;
        }
        else
        {
            lastPos = sword.gameObject.transform.position;
        }
        enemyWeapon = GetComponent<VR_EnemyWeapon>();

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
        // --- 1. 參考座標系轉換 ---
        Vector3 localPlayerPos = transform.InverseTransformPoint(playerTransform.position);
        Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();

        // 取得武器當前變換
        if (interactor_right != null && interactor_right.hasSelection)
        {
            currentWeaponTransform = interactor_right.interactablesSelected[0].transform;
            IsHoldingWeapon = true;
        }
        else
        {
            IsHoldingWeapon = true;
            currentWeaponTransform = sword.gameObject.transform;
        }

        Vector3 localWeaponPos = transform.InverseTransformPoint(currentWeaponTransform.position);
        Vector3 enemyForward = enemyObj.forward;

        // --- 2. 填充傳感器 (直接加入，避免 List 產生 GC) ---
        sensor.AddObservation(enemyForward);                  // 3
        sensor.AddObservation(localPlayerPos);                // 3
        sensor.AddObservation(transform.InverseTransformDirection(playerRb.velocity)); // 3
        sensor.AddObservation(transform.InverseTransformDirection(playerTransform.forward)); // 3
        sensor.AddObservation((int)curState);                 // 1
        sensor.AddObservation(actionProgress);                // 1
        sensor.AddObservation((float)enemy_HP.GetCurrentHealth() / enemy_HP.maxHP); // 1
        sensor.AddObservation((float)player_HP.GetCurrentHealth() / player_HP.maxHP); // 1
        sensor.AddObservation(IsHoldingWeapon ? 1.0f : 0.0f); // 1
        sensor.AddObservation(WeaponVelocity);                // 3
        sensor.AddObservation(sword.CurrentSpeed);         // 1
        sensor.AddObservation(localWeaponPos);                // 3
    }

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
            actionMask.SetActionEnabled(0, 1, false);
            actionMask.SetActionEnabled(0, 2, false);
            actionMask.SetActionEnabled(0, 3, false);
            actionMask.SetActionEnabled(0, 4, false);
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
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        moveDirection = (orientation.right * moveX + orientation.forward * moveZ).normalized;
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            enemyObj.forward = Vector3.Slerp(enemyObj.forward, moveDirection, Time.fixedDeltaTime * rotationSpeed);
        }

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
        int attackAction = actions.DiscreteActions[0];
        // int defendAction = actions.DiscreteActions[2];

        // if (defendAction == 1)
        // {
        //     curState = EnemyState.Defending;
        //     mat.color = Color.blue;
        // }
        if (attackAction > 0 && curState == EnemyState.Idle)
        {
            int attackIndex = attackAction - 1;
            if (attackIndex < comboPatterns.Length)
            {
                ComboData selectedAttack = comboPatterns[attackIndex];

                curState = EnemyState.StartUp;

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

            if (alignment > 0.9f)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerPosition);
                float penalty = -(1.0f / (1.0f + Mathf.Pow(distanceToPlayer, 2))) * player_Behavior.GetActionProgress() * 0.02f;

                AddCustomReward(penalty, "long_attack_penalty");
            }
            // else
            // {
            //     float distanceToPlayer = Vector3.Distance(transform.position, playerPosition);
            //     float reward = 1.0f / (1.0f + Mathf.Pow(distanceToPlayer, 2)) * player_Behavior.GetActionProgress() * 0.02f;

            //     AddCustomReward(reward, "long_attack_reward");
            // }
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

                    float penalty = -(1.0f / (1.0f + Mathf.Pow(distanceToProjectile, 2))) * 0.02f;

                    AddCustomReward(penalty, "avoid_arrow_panalty");
                }
                // else
                // {
                //     float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                //     float reward = 1.0f / (1.0f + Mathf.Pow(distanceToPlayer, 2)) * 0.02f;

                //     AddCustomReward(reward, "avoid_arrow_reward");
                // }
            }
        }

        AddCustomReward(0.00001f, ""); // constant_reward
    }

    // 在 FixedUpdate 中處理物理移動
    void FixedUpdate()
    {
        // 在物理幀中計算武器速度，確保觀測值的物理正確性
        if (currentWeaponTransform != null)
        {
            WeaponVelocity = (currentWeaponTransform.position - lastPos) / Time.fixedDeltaTime;
            lastPos = currentWeaponTransform.position;
        }
        // WeaponAngularVelocity = sword.GetAngularSpeedRad();

        // move
        if (curState != EnemyState.StartUp && curState != EnemyState.Defending)
        {
            if (moveDirection != Vector3.zero)
            {
                Vector3 targetPos = rb.position + moveDirection.normalized * moveSpeed * Time.fixedDeltaTime;
                rb.MovePosition(targetPos);
            }
        }
    }

    public void ResolveEpisode(string result, float timer, float maxTime)
    {
        float timeBonus = (maxTime - timer) / maxTime;
        switch (result)
        {
            case "Enemy":
                AddCustomReward(timeBonus * 0.5f, "timeBonus_reward");
                SetReward(1);
                if (printEndReward)
                {
                    Debug.Log($"Episode End: Player Died (Enemy Wins) Cumulative Reward: {GetCumulativeReward()}");
                }
                break;
            case "Player":
                AddCustomReward(timeBonus * -0.5f, "timeBonus_penalty");
                SetReward(-1);
                if (printEndReward)
                {
                    Debug.Log($"Episode End: Enemy Died (Player Wins) Cumulative Reward: {GetCumulativeReward()}");    
                }
                break;
            case "Draw":
                SetReward(-2);
                if (printEndReward)
                {
                    Debug.Log($"Episode End: Time Out. Cumulative Reward: {GetCumulativeReward()}");    
                }
                break;
            case "Fall":
                SetReward(0f);
                if (printEndReward)
                {
                    Debug.Log($"Episode End: Player fall out of the map. Cumulative Reward: {GetCumulativeReward()}");    
                }
                break;
        }
    }

    IEnumerator Attack(ComboData combo)
    {
        if (printState)
        {
            Debug.Log($"Start Attack : {combo.comboName}");    
        }

        string targetStateName = "";

        // 1. 根據 combo 種類發送 Animator Trigger，並記錄對應的 Animator 狀態方塊名稱
        if (combo.comboName == "stab")
        {
            animator.SetTrigger("Attack1");
            targetStateName = "root|stab";
        }
        else if (combo.comboName == "slash01")
        {
            animator.SetTrigger("Attack2");
            targetStateName = "root|slash01";
        }
        else if (combo.comboName == "slash02")
        {
            animator.SetTrigger("Attack3");
            targetStateName = "root|slash 02";
        }

        if (enemyWeapon != null)
        {
            enemyWeapon.EnableWeaponHitbox(combo.attackSteps[0].damage);
        }
        yield return null;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        while (stateInfo.IsName(targetStateName) && stateInfo.normalizedTime < 1.0f)
        {
            stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            yield return null; 
        }

        if (enemyWeapon != null)
        {
            enemyWeapon.DisableWeaponHitbox();
        }

        if (printState)
        {
            Debug.Log($"Attack Finished : {combo.comboName}, returning to Idle.");
        }
        
        curState = EnemyState.Idle;
    }

    // 用於手動測試，確保動作設定正確
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;

        // 1. 獲取鍵盤實例
        var continuousActions = actionsOut.ContinuousActions;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // --- 手動控制移動 ---
        // int moveAction = 0;
        // if (keyboard.upArrowKey.isPressed) moveAction = 1;
        // else if (keyboard.downArrowKey.isPressed) moveAction = 2;
        // else if (keyboard.leftArrowKey.isPressed) moveAction = 3;
        // else if (keyboard.rightArrowKey.isPressed) moveAction = 4;

        float x = 0f;
        float z = 0f;

        if (keyboard.leftArrowKey.isPressed) x = -1f;
        if (keyboard.rightArrowKey.isPressed) x = 1f;
        if (keyboard.upArrowKey.isPressed) z = 1f;
        if (keyboard.downArrowKey.isPressed) z = -1f;

        // --- 手動控制攻擊 ---
        int attackAction = 0;
        if (comboPatterns != null && comboPatterns.Length > 0)
        {
            if (keyboard.digit1Key.isPressed) attackAction = 1;
            else if (comboPatterns.Length > 1 && keyboard.digit2Key.isPressed) attackAction = 2;
            else if (comboPatterns.Length > 2 && keyboard.digit3Key.isPressed) attackAction = 3;
            else if (comboPatterns.Length > 3 && keyboard.digit4Key.isPressed) attackAction = 4;
        }

        // 2. 將數值填入對應的通道 (必須對應 Behavior Parameters 的 Branches)
        //discreteActions[0] = moveAction;
        continuousActions[0] = x;
        continuousActions[1] = z;
        discreteActions[0] = attackAction;
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
        AddCustomReward(damageDealt / 50.0f, "damage_reward");
    }
    public void OnDamageTakenFromMelee(int damageTaken)
    {
        AddCustomReward(-damageTaken / 60.0f, "damage_from_melee_penalty");
    }
    public void OnDamageTakenFromRanged(int damageTaken)
    {
        AddCustomReward(-damageTaken / 50.0f, "damage_from_ranged_penalty");
    }
    public void OnPlayerLongAttackMissed()
    {
        // AddReward(0.2f);
    }
    private void AddCustomReward(float value, string reason = "")
    {
        AddReward(value);
        if (printReward && !string.IsNullOrEmpty(reason))
        {
            Debug.Log($"[Reward] {reason}: {value}");
        }
    }


    // 玩家瞄準範圍(如果敵人在紅範圍會受到懲罰)
    private void OnDrawGizmos()
    {
        if (player_Behavior == null || !Application.isPlaying) return;

        if (player_Behavior.IsAiming())
        {
            // --- 1. 定义锥形的参数 ---
            float alignmentThreshold = 0.9f; // 你的阈值
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
            float alignmentThreshold = 0.95f;
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
