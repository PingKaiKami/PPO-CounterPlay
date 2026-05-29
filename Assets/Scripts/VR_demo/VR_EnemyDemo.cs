using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class VR_EnemyDemo : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        StartUp,
        Recovery,
        Defending,
        Dead // 新增：死亡狀態
    }

    public enum EnemyMode
    {
        Manual,           
        ChasingAttack,    
        CounterAttack,    
        StationaryGuard   
    }

    [Header("Behavior Settings")]
    public EnemyMode currentMode = EnemyMode.Manual;
    public EnemyState curState = EnemyState.Idle;
    public float moveSpeed = 3f;
    public float rotationSpeed = 10f; 

    [Header("AI Attack Settings")]
    public float attackRange = 2f;
    public float aiAttackCooldown = 2f;
    private float nextAiAttackTime;

    [Header("References")]
    public Transform playerTransform;
    public XRBaseInteractor interactor_right;
    public VR_SwordDemo sword;
    public Animator animator;
    
    [Header("Attack Patterns")]
    public ComboData[] comboPatterns;

    [Header("Debug")]
    public bool printState = false;

    // 唯讀外部屬性
    public float WeaponVelocityMagnitude => weaponVelocity.magnitude;
    public EnemyState GetEnemyState() => curState;

    // 內部物理與組件變數
    private Vector3 weaponVelocity;
    private Vector3 lastPos;
    private Transform currentWeaponTransform;
    private VR_EnemyWeapon enemyWeapon;
    private VR_HP_Enemy enemy_HP;
    private Rigidbody rb;
    private Vector3 moveDirection;
    private Vector3 lookDirection;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    
    // 追蹤當前攻擊的協程，方便隨時中斷
    private Coroutine currentAttackCoroutine;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        enemyWeapon = GetComponent<VR_EnemyWeapon>();
        enemy_HP = GetComponent<VR_HP_Enemy>();
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (interactor_right != null && interactor_right.hasSelection)
        {
            currentWeaponTransform = interactor_right.interactablesSelected[0].transform;
            lastPos = currentWeaponTransform.position;
        }
        else if (sword != null)
        {
            currentWeaponTransform = sword.gameObject.transform;
            lastPos = currentWeaponTransform.position;
        }
    }

    void Update()
    {
        // 如果已經死亡，切斷所有行為大腦
        if (curState == EnemyState.Dead) return;

        // 檢查是否死亡（實時監測血量組件）
        if (enemy_HP != null && enemy_HP.IsDead())
        {
            TriggerDeath();
            return;
        }

        switch (currentMode)
        {
            case EnemyMode.Manual:
                HandleManualInput();
                break;
            case EnemyMode.ChasingAttack:
                ExecuteChasingAttackLogic();
                break;
            case EnemyMode.CounterAttack:
                ExecuteCounterAttackLogic();
                break;
            case EnemyMode.StationaryGuard:
                ExecuteStationaryGuardLogic();
                break;
        }
    }

    void FixedUpdate()
    {
        if (curState == EnemyState.Dead) return;

        // 1. 計算武器速度
        if (currentWeaponTransform != null)
        {
            weaponVelocity = (currentWeaponTransform.position - lastPos) / Time.fixedDeltaTime;
            lastPos = currentWeaponTransform.position;
        }

        // 2. 處理敵人移動與轉向（StartUp、Defending、Dead 狀態下無法移動）
        if (curState != EnemyState.StartUp && curState != EnemyState.Defending && curState != EnemyState.Dead)
        {
            // 【物理移動】不論朝哪，依然根據 moveDirection 移動
            if (moveDirection != Vector3.zero)
            {
                Vector3 targetPos = rb.position + moveDirection.normalized * moveSpeed * Time.fixedDeltaTime;
                rb.MovePosition(targetPos);
            }

            // 【物理旋轉】改用 lookDirection 來決定面朝方向！
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized);
                Quaternion smoothedRotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
                rb.MoveRotation(smoothedRotation);
            }
        }
    }

    // --- 【核心新增功能一：觸發彈刀/格擋】 ---
    /// <summary>
    /// 當武器砍到玩家武器、或是被玩家完美招架時，由外部（如 VR_EnemyWeapon）呼叫此函式
    /// </summary>
    public void TriggerWeaponClashGuard()
    {
        if (curState == EnemyState.Dead) return;

        if (printState) Debug.Log("【武器衝突】觸發格擋，該次攻擊取消！");

        // 1. 立刻中斷當前的攻擊協程（讓原本的攻擊邏輯不再往下走，傷害不算數）
        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
        }

        // 2. 確保關閉武器傷害判定框（防呆，避免中斷時判定框殘留）
        if (enemyWeapon != null)
        {
            enemyWeapon.DisableWeaponHitbox();
        }

        // 3. 切換至格擋狀態，並觸發動畫
        curState = EnemyState.Defending;
        
        if (animator != null)
        {
            animator.SetTrigger("Guard");
        }

        // 4. 開啟一個短暫的小協程，等待格擋動畫播完（或設定固定時間）後切回 Idle
        StartCoroutine(GuardRecoveryRoutine());
    }

    private IEnumerator GuardRecoveryRoutine()
    {
        // 這裡暫定格擋僵直時間為 0.5 秒，你也可以改成讀取動畫長度
        yield return new WaitForSeconds(0.5f);
        
        if (curState == EnemyState.Defending)
        {
            curState = EnemyState.Idle;
        }
    }

    // --- 【核心新增功能二：觸發死亡】 ---
    private void TriggerDeath()
    {
        curState = EnemyState.Dead;
        moveDirection = Vector3.zero;

        // 停止所有動作
        StopAllCoroutines();

        if (enemyWeapon != null) enemyWeapon.DisableWeaponHitbox();

        if (animator != null)
        {
            animator.SetTrigger("Death");
        }

        if (printState) Debug.Log("【敵人死亡】觸發 Death 動畫。");
    }


    #region AI 模式邏輯區 (Manual, Chasing, Counter, Guard)
    private void HandleManualInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float x = 0f;
        float z = 0f;

        if (keyboard.leftArrowKey.isPressed) x = -1f;
        if (keyboard.rightArrowKey.isPressed) x = 1f;
        if (keyboard.upArrowKey.isPressed) z = 1f;
        if (keyboard.downArrowKey.isPressed) z = -1f;

        moveDirection = new Vector3(x, 0f, z);
        lookDirection = moveDirection;

        if (curState == EnemyState.Idle && comboPatterns != null && comboPatterns.Length > 0)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) CheckAndStartAttack(0);
            else if (comboPatterns.Length > 1 && keyboard.digit2Key.wasPressedThisFrame) CheckAndStartAttack(1);
            else if (comboPatterns.Length > 2 && keyboard.digit3Key.wasPressedThisFrame) CheckAndStartAttack(2);
            else if (comboPatterns.Length > 3 && keyboard.digit4Key.wasPressedThisFrame) CheckAndStartAttack(3);
        }
    }

    private void ExecuteChasingAttackLogic()
    {
        if (playerTransform == null) return;

        Vector3 playerPlanePos = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        float distance = Vector3.Distance(transform.position, playerPlanePos);
        Vector3 directionToPlayer = (playerPlanePos - transform.position).normalized;

        lookDirection = directionToPlayer;

        if (curState == EnemyState.Idle)
        {
            if (distance > attackRange)
            {
                moveDirection = directionToPlayer;
            }
            else
            {
                moveDirection = Vector3.zero;

                if (Time.time >= nextAiAttackTime)
                {
                    int randomAttack = Random.Range(0, comboPatterns.Length);
                    CheckAndStartAttack(randomAttack);
                    nextAiAttackTime = Time.time + aiAttackCooldown;
                }
            }
        }
    }
    private void ExecuteCounterAttackLogic()
    {
        if (playerTransform == null) return;

        // 1. 計算與玩家的平面距離與方向
        Vector3 playerPlanePos = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        float distance = Vector3.Distance(transform.position, playerPlanePos);
        Vector3 directionToPlayer = (playerPlanePos - transform.position).normalized;

        // 不論前進或後退，眼睛都死死盯著玩家
        lookDirection = directionToPlayer;

        if (curState == EnemyState.Idle)
        {
            // 2. 核心邏輯分支：檢查攻擊是否還在冷卻中
            bool isCooldown = Time.time < nextAiAttackTime;

            if (isCooldown)
            {
                // 【情境 A：還在冷卻中】➔ 目標是與玩家保持 2 * attackRange 的安全間距
                float safeDistance = attackRange * 2f;

                if (distance < safeDistance * 0.9f)
                {
                    // 離玩家太近了（小於安全距離的 90%），必須「往後退」
                    moveDirection = -directionToPlayer;
                    
                    // 觸發倒退動畫
                    if (animator != null) animator.SetBool("GoBack", true);
                }
                else if (distance > safeDistance * 1.1f)
                {
                    moveDirection = Vector3.zero;
                    if (animator != null) animator.SetBool("GoBack", true);
                }
            }
            else
            {
                // 【情境 B：冷卻好了！】➔ 瘋狂追擊玩家，直到踏入 attackRange
                if (animator != null) animator.SetBool("GoBack", false); // 衝鋒時不播倒退動畫

                if (distance > attackRange)
                {
                    // 還沒進入攻擊範圍，全速「主動追擊」
                    moveDirection = directionToPlayer;
                }
                else
                {
                    // 成功突進到 attackRange 內！停下腳步立刻出招
                    moveDirection = Vector3.zero;

                    // 發動反擊 combo，並設定下一次的冷卻時間
                    int randomAttack = Random.Range(0, comboPatterns.Length);
                    CheckAndStartAttack(randomAttack);
                    
                    // 進入冷卻
                    nextAiAttackTime = Time.time + aiAttackCooldown;
                }
            }
        }
        else
        {
            // 非 Idle 狀態（例如正在揮刀或格擋僵直），關閉倒退動畫
            if (animator != null) animator.SetBool("GoBack", false);
        }
    }

    private void ExecuteStationaryGuardLogic()
    {
        if (playerTransform == null) return;

        Vector3 playerPlanePos = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        float distance = Vector3.Distance(transform.position, playerPlanePos);
        Vector3 directionToPlayer = (playerPlanePos - transform.position).normalized;

        moveDirection = Vector3.zero;
        lookDirection = directionToPlayer;

        if (curState == EnemyState.Idle)
        {
            if (directionToPlayer != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(directionToPlayer);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            if (distance <= attackRange && Time.time >= nextAiAttackTime)
            {
                int randomAttack = Random.Range(0, comboPatterns.Length);
                CheckAndStartAttack(randomAttack);
                nextAiAttackTime = Time.time + aiAttackCooldown;
            }
        }
    }
    #endregion

    private void CheckAndStartAttack(int index)
    {
        if (comboPatterns != null && index < comboPatterns.Length && comboPatterns[index] != null)
        {
            curState = EnemyState.StartUp;
            // 儲存協程參照
            currentAttackCoroutine = StartCoroutine(Attack(comboPatterns[index]));
        }
    }

    IEnumerator Attack(ComboData combo)
    {
        if (printState) Debug.Log($"Start Attack : {combo.comboName}");    

        string targetStateName = "";

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
            targetStateName = "root|slash02";
        }

        if (enemyWeapon != null && combo.attackSteps != null)
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

        if (enemyWeapon != null) enemyWeapon.DisableWeaponHitbox();

        if (printState) Debug.Log($"Attack Finished : {combo.comboName}, returning to Idle.");
        
        curState = EnemyState.Idle;
        currentAttackCoroutine = null; // 正常播完，清空參照
    }

    public void ResetEnemy()
    {
        StopAllCoroutines();
        currentAttackCoroutine = null;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.position = initialPosition;
        transform.rotation = initialRotation;

        if (currentWeaponTransform != null)
        {
            lastPos = currentWeaponTransform.position;
        }

        curState = EnemyState.Idle;
        moveDirection = Vector3.zero;
        nextAiAttackTime = 0f;

        if (enemy_HP != null) enemy_HP.ResetHealth();
        if (enemyWeapon != null) enemyWeapon.DisableWeaponHitbox();

        if (animator != null)
        {
            animator.Rebind(); 
            animator.Update(0f);
        }
    }
}