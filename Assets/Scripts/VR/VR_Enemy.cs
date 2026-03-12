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

public class VR_Enemy : Agent
{
    public int health;
    public Transform playerCamera;
    public XRBaseInteractor interactor_right;
    public Vector3 WeaponAngularVelocity { get; private set; }
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
    public VR_Sword sword;
    public MeshRenderer mr;
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
    private VR_TrainingArea myArea;
    private bool isHit = false;
    public float GetActionProgress() => actionProgress;
    public bool GetIsEnemyGotHit() => isHit;

    private void OnTriggerEnter(Collider other) 
    {
        if(other.CompareTag("Sword"))
            isHit = true;
    }
    private void OnTriggerExit(Collider other) 
    {
        if(other.gameObject.name == "Sword")
            isHit = false;
    }

    private void CalculatePhysics(Transform target)
    {
        WeaponVelocity = (target.position - lastPos) / Time.deltaTime;
        lastPos = target.position;
    }

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
        myArea = GetComponentInParent<VR_TrainingArea>();
        if (interactor_right != null && interactor_right.hasSelection)
        {
            lastPos = interactor_right.interactablesSelected[0].transform.position;
        }
        else
        {
            lastPos = sword.gameObject.transform.position;
        }
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
        Vector3 toWeapon = lastPos - transform.position;
        Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
        Vector3 forwardDir; // for camera same as playerForwared

        // for manual
        if (interactor_right != null && interactor_right.hasSelection)
        {
            var interactable = interactor_right.interactablesSelected[0];
            currentWeaponTransform = interactable.transform;
            IsHoldingWeapon = true;
            forwardDir = playerCamera.forward;
            CameraRotationAngles = playerCamera.eulerAngles;
        }
        // for training
        else
        {
            IsHoldingWeapon = true;
            currentWeaponTransform = sword.gameObject.GetComponent<VR_Sword>().swordTip;
            forwardDir = playerTransform.forward;
            CameraRotationAngles = playerTransform.eulerAngles;
        }
        CalculatePhysics(currentWeaponTransform);

        Vector3 enemyForward = enemyObj.forward.normalized;
        obs.AddRange(new float[] { enemyForward.x, enemyForward.y, enemyForward.z });
        Vector3 toPlayerDir = toPlayer.normalized;
        obs.AddRange(new float[] { toPlayerDir.x, toPlayerDir.y, toPlayerDir.z });
        obs.Add(toPlayer.magnitude);
        Vector3 playerVel = playerRb != null ? playerRb.velocity : Vector3.zero;
        obs.AddRange(new float[] { playerVel.x, playerVel.y, playerVel.z });
        Vector3 playerForward = playerTransform.forward.normalized;
        obs.AddRange(new float[] { playerForward.x, playerForward.y, playerForward.z });
        // obs.Add((int)player_Behavior.GetCurrentState()); //cheat
        obs.Add((int)curState);
        // obs.Add(player_Behavior.GetActionProgress()); //cheat
        obs.Add(actionProgress);
        obs.Add((float)enemy_HP.GetCurrentHealth() / enemy_HP.maxHP);
        obs.Add((float)player_HP.GetCurrentHealth() / player_HP.maxHP);
        //for VR
        obs.Add(IsHoldingWeapon ? 1.0f : 0.0f);
        // obs.Add(forwardDir) // represent with playerForward
        // obs.AddRange(new float[] { CameraRotationAngles.x, CameraRotationAngles.y, CameraRotationAngles.z }); //later on
        obs.AddRange(new float[] { WeaponVelocity.x, WeaponVelocity.y, WeaponVelocity.z });
        // obs.AddRange(new float[] { WeaponAngularVelocity.x, WeaponAngularVelocity.y, WeaponAngularVelocity.z });
        Vector3 toWeaponDir = toWeapon.normalized;
        obs.AddRange(new float[] { toWeaponDir.x, toWeaponDir.y, toWeaponDir.z });
        obs.Add(toWeapon.magnitude);

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

    // 在 FixedUpdate 中檢查結束條件是個好習慣，因為它與物理更新同步
    void FixedUpdate()
    {
        episodeTimer += Time.fixedDeltaTime;

        // 條件 a: 玩家死亡 (敵人獲勝)
        if (player_HP != null && player_HP.IsDead())
        {
            float timeBonus = (maxEpisodeTime - episodeTimer) / maxEpisodeTime;
            AddCustomReward(timeBonus * 0.5f, "timeBonus_reward");
            AddCustomReward(enemy_HP.CurrentHP / 100, "health_reward_ending");
            SetReward(1);
            if (printEndReward)
            {
                Debug.Log($"Episode End: Player Died (Enemy Wins) Cumulative Reward: {GetCumulativeReward()}");
            }
            if (BenchmarkManager.Instance != null && isVerify)
            {
                BenchmarkManager.Instance.RecordEpisode(modelName, "Enemy", GetCumulativeReward());
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
            if (BenchmarkManager.Instance != null && isVerify)
            {
                BenchmarkManager.Instance.RecordEpisode(modelName, "Player", GetCumulativeReward());
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
            if (BenchmarkManager.Instance != null && isVerify)
            {
                BenchmarkManager.Instance.RecordEpisode(modelName, "Draw", GetCumulativeReward());
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
    private void EndAndCleanupEpisode()
    {
        if (myArea != null)
        {
            myArea.TriggerEpisodeEnd();
        }
        var playerAgent = playerTransform.GetComponent<VR_Player_Agent>();
        if(playerAgent != null)
        {
            playerAgent.EndEpisode();
        }
        EndEpisode();
    }

    IEnumerator Attack(ComboData combo)
    {
        if (printState)
        {
            Debug.Log($"Start Attack : {combo.comboName}");    
        }

        float time = 0f;
        foreach (AttackData step in combo.attackSteps)
        {
            attackRange_enemy.UpdateAttackShape(step);
            // player_Behavior.ChangeDistanceToEnemy(step.attackRange * 1.2f);

            time = 0f;
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
                    if (player_Behavior.GetCurrentState() == VR_Player_Behavior.PlayerState.Defending)
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

            if(step.recoveryTime != 0)
            {
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
            }

            actionProgress = 0f;
            mat.color = oriColor;
            curState = EnemyState.Idle;
        }

        // time = 0f;
        // while(time < combo.comboCooldown)
        // {
        //     time += Time.fixedDeltaTime;
        //     yield return new WaitForFixedUpdate();
        // }
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
        AddCustomReward(-damageTaken / 50.0f, "damage_from_melee_penalty");
    }
    public void OnDamageTakenFromRanged(int damageTaken)
    {
        AddCustomReward(-damageTaken / 50.0f, "damage_from_ranged_penalty");
    }
    public void OnPlayerAttackMissed()
    {
        AddCustomReward(0.2f, "avoid_attack_reward");
    }
    public void OnPlayerLongAttackMissed()
    {
        // AddReward(0.2f);
    }

    private float currentStepReward = 0f;

    private void AddCustomReward(float value, string reason = "")
    {
        currentStepReward += value;
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
