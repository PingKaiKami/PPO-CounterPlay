using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem.XR;

using Unity.VisualScripting;
using System.Text;

public class VR_Player_Behavior : MonoBehaviour
{
    public enum PlayerState { Idle, Attacking, Aiming, Recovery, Defending, Waiting, Dashing }
    public enum TrainingMode { AutoTrace, OnlyDash, AttackAndRun, AttackAndDash, SmartAttack, LongRangeAttack, DashAtkRanged, AtkDashRanged, Escape, Random, GAIL, Manual }

    [Header("References")]
    [SerializeField] private ThirdPersonCamera thirdPersonCamera;
    public GameObject enemy;
    public Transform target;
    public Transform orientation;
    public XROrigin xrOrigin;
    [SerializeField] private NearFarInteractor rightController;
    [SerializeField] private GameObject sword_object;
    [SerializeField] private TrackedPoseDriver handPoseDriver; // 手的 Driver
    [SerializeField] private GameObject FP_Camera;
    [SerializeField] private GameObject TP_Camera_main;
    [SerializeField] private GameObject TP_Camera_freelook;

    [Header("Behavior Settings")]
    public TrainingMode curMode;

    [Header("Movement & Dash")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float dashSpeed = 30f;
    public float dashDuration = 0.1f;
    public float dashCooldown = 1f;

    [Header("Attack")]
    public int damage = 15;
    public float strafeRadius = 5f;
    public float attackBodyRange = 0.5f; // 因為敵人也有體積，所以加這個彌補
    public float attackBufferRange = 0.5f; // 讓玩家再靠近敵人一點再攻擊 (目前太常揮空了)
    public float attackStartUpTime = 0.5f;
    public float attackTime = 1f;
    public float attackRecoveryTime = 1f;

    [Header("LongAttack")]
    public GameObject arrowPrefab;
    public float aimingMoveSpeed = 1f;
    public float aimingRotationSpeed = 3f;
    public int longAttackDamage = 20; // max value, actually will decrease a little depends on aimming time (min = damage/2)
    public float longAttackStartUpBasicTime = 2f; // actually minus 1 (for manual)
    public float longAttackStartUpBonusTime = 3f; // extra 0 ~ bonus startUpTime
    public float longAttackRecoveryTime = 1f;
    public float longAttackRange = 5f;
    public float arrowSpeed = 1f; // // max value, actually will decrease a little depends on aimming time (min = arrowSpeed/2)

    [Header("AI Decision Making")]
    public float strafeSpeed = 1.5f;

    [Range(0.01f, 1.0f)]
    public float waitingRecoveryChance = 0.1f;
    private float waitingTime = 0;

    [Header("Debug")]
    public PlayerState CurrentState = PlayerState.Idle;
    public bool IsUseInternalLogic = false;
    public bool printState = false;

    [Header("Data Recording")]
    public bool isLogging = false; // 開關：是否開始錄製數據
    private string csvPath;
    private float lastStrafeInput = 0f; // 紀錄最後一次的側移量

    protected Rigidbody rb;
    protected AttackRange attackRange;
    protected float attackRadius;
    protected Material mat_player;
    protected Material mat_line;
    private bool _isDashing = false;
    private bool _canDash = true;
    private bool _isAiming = false;
    private int _clockwise = 0;
    private bool _isSmartAtk = false;
    private bool _isDashAtk = false;
    private bool _isAtkDash = false;
    private bool isRandom = false;
    private bool isGail = false;
    private bool isManual = false;
    private bool isForTraining = false;
    private List<GameObject> _activeProjectiles = new List<GameObject>();
    public IReadOnlyList<GameObject> ActiveProjectiles => _activeProjectiles;
    private float _actionProgress = 0f;
    private float _internalAttackTimer = -1f; // -1 表示不在攻擊中
    private int decisionTime;
    private Vector3 _currentSwordPos;
    private Quaternion _currentSwordRot;
    private Vector3 swordVelocity; // 儲存 SmoothDamp 用
    private LineRenderer aimLine;
    private VR_TrainingArea myArea;
    private Transform projectileParent;
    public GameObject activateSword { get; private set; }
    private Vector3 swordOriPos;
    public PlayerState GetCurrentState() => CurrentState;
    private StringBuilder _logCache = new StringBuilder();
    public bool IsAiming() => _isAiming;
    public float GetActionProgress() => _actionProgress;
    public bool CanDash() => _canDash;
    public bool IsEnemyInAttackRange(GameObject e) => attackRange != null && attackRange.IsSpecificEnemyInRange(e);

    protected void Initialize()
    {
        aimLine = GetComponent<LineRenderer>();
        if (aimLine != null)
        {
            aimLine.positionCount = 2;
            aimLine.enabled = false;
            mat_line = aimLine.material;
        }
        rb = GetComponent<Rigidbody>();
        attackRange = GetComponentInChildren<AttackRange>();
        if (attackRange != null) attackRadius = attackRange.radius;
        var renderer = GetComponentInChildren<MeshRenderer>();
        if (renderer != null) mat_player = renderer.material;
        myArea = GetComponentInParent<VR_TrainingArea>();
        if(rightController != null)
        {
            isForTraining = false;
            activateSword = rightController.gameObject;
        }
        else
        {
            isForTraining = true;
            activateSword = sword_object;
        }
        swordOriPos = activateSword.transform.localPosition;

        if (myArea != null)
        {
            myArea.OnEpisodeEnd += ResetStateToIdle;

            projectileParent = myArea.transform.Find("Projectile_Parent");
            if (projectileParent == null)
            {
                projectileParent = new GameObject("Projectile_Parent").transform;
                projectileParent.SetParent(myArea.transform);
            }
        }
        if (curMode == TrainingMode.Random)
        {
            isRandom = true;
            SetRandomTrainingMode();
        }
        else if(curMode == TrainingMode.GAIL)
        {
            isGail = true;
            SetRandomTrainingMode();
        }
        // camera setting for vr
        if(curMode == TrainingMode.Manual)
        {
            isManual = true;
            FP_Camera.SetActive(true);
            TP_Camera_main.SetActive(false);
            TP_Camera_freelook.SetActive(false);
        }
        else
        {
            if(FP_Camera != null && TP_Camera_main != null && TP_Camera_freelook != null)
            {
                FP_Camera.SetActive(false);
                TP_Camera_main.SetActive(true);
                TP_Camera_freelook.SetActive(true);
            }
        }
        decisionTime = GetDecisionPeriod();
        _currentSwordPos = Vector3.zero;
        _currentSwordRot = Quaternion.identity;
        if (isLogging)
        {
            // 初始化 CSV 存檔路徑 (存放在專案根目錄的 DataLogs 資料夾)
            string folderPath = Application.dataPath + "/DataLogs";
            if (!System.IO.Directory.Exists(folderPath)) System.IO.Directory.CreateDirectory(folderPath);
            
            // 根據目前的模式給予檔名，例如：Manual_Data.csv 或 GAIL_Data.csv
            string modeLabel = IsUseInternalLogic ? "Human" : "AI";
            csvPath = folderPath + $"/{modeLabel}_BattleData.csv";

            // 如果檔案不存在，寫入表頭
            if (!System.IO.File.Exists(csvPath))
            {
                System.IO.File.WriteAllText(csvPath, "Label,Distance,StrafingIntensity,AngularVelocity\n");
            }
        }
    }
    public void ResetStateToIdle()
    {
        StopAllCoroutines();

        if (isRandom || isGail)
        {
            SetRandomTrainingMode();
        }

        CurrentState = PlayerState.Idle;
        _isDashing = false;
        _canDash = true;
        _isAiming = false;
        transform.localPosition = new Vector3(0, 0.5f, 0);

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (mat_player != null)
        {
            mat_player.color = Color.white;
        }
        if(rightController != null)
        {
            StartCoroutine(AutoEquipWeapon(rightController, sword_object.GetComponent<XRGrabInteractable>(), isManual));
        }
    }
    private void OnDestroy()
    {
        if (myArea != null)
        {
            myArea.OnEpisodeEnd -= ResetStateToIdle;
        }
    }
    // main
    protected void UpdateBehavior()
    {
        if (activateSword != null) {
            activateSword.transform.localPosition = Vector3.SmoothDamp(
                activateSword.transform.localPosition, 
                _currentSwordPos, 
                ref swordVelocity, 
                0.08f 
            );

            activateSword.transform.localRotation = Quaternion.Slerp(
                activateSword.transform.localRotation,
                _currentSwordRot,
                Time.fixedDeltaTime * 15f
            );
        }
        
        // 這裡的邏輯門保持不變
        if (!IsUseInternalLogic || curMode == TrainingMode.GAIL) return;
        if (_isDashing) return;

        // A. 思考 (大腦會算出移動方向 + 武器該放在哪)
        AI_Intent intent = GetLogicIntent(curMode);

        // B. 執行 (把所有意圖丟進去執行)
        ExecuteAgentAction(
            intent.moveDirection, 
            intent.lookDirection, 
            intent.wantsDash, 
            intent.swordTargetLocalPos,
            intent.swordTargetLocalRot
        );
    }
    public struct AI_Intent
    {
        public Vector3 moveDirection;
        public Vector3 lookDirection;
        public bool wantsAttack;
        public bool wantsDash;
        public Vector3 swordTargetLocalPos;
        public Quaternion swordTargetLocalRot;
    }

    public AI_Intent GetLogicIntent(TrainingMode mode)
    {
        AI_Intent intent = new AI_Intent();
        float timeStep = Time.fixedDeltaTime;
    
        // 如果不是使用內部邏輯 (代表現在是由 Agent 的 Heuristic 在取樣)
        if (!IsUseInternalLogic)
        {
            // 我們必須把時間補償回來，否則計時器會變慢 5 倍
            timeStep *= decisionTime;
        }
        
        // --- 基礎資訊 ---
        Vector3 directionToTarget = Vector3.zero;
        float distanceToTarget = 0f;
        if (target != null)
        {
            directionToTarget = target.position - transform.position;
            directionToTarget.y = 0;
            distanceToTarget = directionToTarget.magnitude;
        }

        // =========================================================
        // Part 1: 移動意圖 (Movement)
        // =========================================================
        float atkRadius = attackRadius + attackBodyRange - attackBufferRange;
        switch (mode)
        {
            case TrainingMode.Manual:
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    float h = 0; float v = 0;
                    if (keyboard.aKey.isPressed) h = -1f;
                    if (keyboard.dKey.isPressed) h = 1f;
                    if (keyboard.wKey.isPressed) v = 1f;
                    if (keyboard.sKey.isPressed) v = -1f;

                    // 直接拿相機的 Forward，不用擔心迴圈問題了！
                    Vector3 camF = xrOrigin.Camera.transform.forward;
                    Vector3 camR = xrOrigin.Camera.transform.right;
                    camF.y = 0; camR.y = 0;

                    intent.moveDirection = (camF.normalized * v + camR.normalized * h).normalized;
                    
                    // lookDirection 設為跟移動方向一致即可，或是設為 camF
                    intent.lookDirection = camF.normalized;
                }
                break;

            case TrainingMode.AutoTrace:
                intent.moveDirection = (distanceToTarget > atkRadius) ? directionToTarget : Vector3.zero;
                break;

            case TrainingMode.AttackAndRun:
            case TrainingMode.AttackAndDash:
                if (CurrentState == PlayerState.Idle)
                    intent.moveDirection = (distanceToTarget > atkRadius) ? directionToTarget : Vector3.zero;
                else
                    intent.moveDirection = (distanceToTarget < strafeRadius) ? -directionToTarget : GetStrafeDirection(directionToTarget);
                break;

            case TrainingMode.SmartAttack:
                var enemyAgent = enemy.GetComponent<VR_Enemy>();
                bool enemyInRecovery = (enemyAgent != null && enemyAgent.GetEnemyState() == VR_Enemy.EnemyState.Recovery);
                
                // 如果在 Idle，且敵人進入 Recovery，則觸發 SmartAttack
                if (CurrentState == PlayerState.Idle && enemyInRecovery)
                {
                    _isSmartAtk = true;
                }
                
                // 如果成功發起攻擊，或者敵人距離太遠但並未在發起 SmartAttack 過程中，則取消狀態 (在動作邏輯區處理)

                if (_isSmartAtk)
                    intent.moveDirection = (distanceToTarget > atkRadius) ? directionToTarget : Vector3.zero;
                else
                    intent.moveDirection = (distanceToTarget < strafeRadius) ? -directionToTarget : GetStrafeDirection(directionToTarget);
                break;

            case TrainingMode.LongRangeAttack:
                intent.moveDirection = (distanceToTarget < longAttackRange) ? -directionToTarget : 
                                        (distanceToTarget > longAttackRange * 1.5f) ? directionToTarget : 
                                        GetStrafeDirection(directionToTarget);
                break;
            case TrainingMode.DashAtkRanged:
                if(_isDashAtk)
                    intent.moveDirection = (distanceToTarget > atkRadius) ? directionToTarget : Vector3.zero;
                else
                    intent.moveDirection = (distanceToTarget < longAttackRange) ? -directionToTarget : 
                                        (distanceToTarget > longAttackRange * 1.5f) ? directionToTarget : 
                                        GetStrafeDirection(directionToTarget);
                break;

            case TrainingMode.AtkDashRanged:
                if (_isAtkDash)
                    intent.moveDirection = (distanceToTarget > atkRadius) ? directionToTarget : Vector3.zero;
                else
                    intent.moveDirection = (distanceToTarget < longAttackRange) ? -directionToTarget : 
                                        (distanceToTarget > longAttackRange * 1.5f) ? directionToTarget : 
                                        GetStrafeDirection(directionToTarget);
                break;

            case TrainingMode.Escape:
            case TrainingMode.OnlyDash:
                intent.moveDirection = -directionToTarget;
                break;

            default:
                intent.moveDirection = Vector3.zero;
                break;
        }

        // =========================================================
        // Part 2: 旋轉意圖 (Rotation) - 補回原本的邏輯
        // =========================================================
        if (mode == TrainingMode.Manual)
        {
            if (thirdPersonCamera)
            {
                if (_isAiming || Input.GetMouseButton(1)) // 這裡加強判斷
                {
                    Vector3 mouseWorldPos = GetMouseWorldPosition();
                    thirdPersonCamera.enabled = false;
                    if (mouseWorldPos != Vector3.zero)
                        intent.lookDirection = mouseWorldPos - transform.position;
                    else
                        intent.lookDirection = intent.moveDirection;
                }
                else
                {
                    thirdPersonCamera.enabled = true;
                    intent.lookDirection = intent.moveDirection;
                }
            }
            else
            {
                intent.lookDirection = intent.moveDirection;
            }
        }
        else
        {
            // AI 模式：預設看移動方向
            intent.lookDirection = directionToTarget;
        }

        // =========================================================
        // Part 3: 動作意圖 (Action)
        // =========================================================
        bool canAct = (CurrentState == PlayerState.Idle || CurrentState == PlayerState.Recovery);

        if (canAct)
        {
            switch (mode)
            {
                case TrainingMode.Manual:
                    var mouse = Mouse.current; // 取得滑鼠實例
                    var keyboard = Keyboard.current;

                    if (mouse.leftButton.isPressed) intent.wantsAttack = true;
                    // if (mouse.rightButton.isPressed) intent.wantsLongAttack = true; 
                    if (keyboard.leftShiftKey.isPressed) intent.wantsDash = true;
                    break;

                case TrainingMode.AutoTrace:
                case TrainingMode.AttackAndRun:
                    if (CurrentState == PlayerState.Idle && distanceToTarget <= atkRadius) 
                        intent.wantsAttack = true;
                    break;

                case TrainingMode.AttackAndDash:
                    if (CurrentState == PlayerState.Idle)
                    {
                        if (distanceToTarget <= atkRadius) intent.wantsAttack = true;
                    }
                    else if (_canDash) intent.wantsDash = true;
                    break;

                case TrainingMode.SmartAttack:
                    if (CurrentState == PlayerState.Idle && _isSmartAtk)
                    {
                        if (distanceToTarget <= atkRadius)
                        {
                            intent.wantsAttack = true;
                            _isSmartAtk = false; // 攻擊後重置狀態
                        }
                        else if (_canDash) 
                        {
                            intent.wantsDash = true;
                        }
                    }
                    break;

                // case TrainingMode.LongRangeAttack:
                //     if (CurrentState == PlayerState.Idle && distanceToTarget >= longAttackRange) 
                //         intent.wantsLongAttack = true;
                //     break;

                // case TrainingMode.DashAtkRanged:
                //     if (CurrentState == PlayerState.Idle)
                //     {
                //         if(distanceToTarget >= longAttackRange)
                //         {
                //             if (Random.value > 0.5f && !_isDashAtk) intent.wantsLongAttack = true;
                //             else _isDashAtk = true;
                //         }
                //         else if(distanceToTarget <= atkRadius)
                //         {
                //             intent.wantsAttack = true;
                //         }
                //     }
                //     else if(_isDashAtk && _canDash)
                //     {
                //         intent.wantsDash = true;
                //         _isDashAtk = false;
                //     }
                //     break;

                // case TrainingMode.AtkDashRanged:
                //     if (CurrentState == PlayerState.Idle)
                //     {
                //         if (distanceToTarget >= longAttackRange)
                //         {
                //             if (Random.value > 0.5f && !_isAtkDash) intent.wantsLongAttack = true;
                //             else _isAtkDash = true;
                //         }
                //         else if (distanceToTarget <= atkRadius)
                //         {
                //             intent.wantsAttack = true;
                //         }
                //     }
                //     else if (_isAtkDash && _canDash)
                //     {
                //         intent.wantsDash = true;
                //         _isAtkDash = false;
                //     }
                //     break;

                case TrainingMode.OnlyDash:
                    if (_canDash) intent.wantsDash = true;
                    break;
            }
        }

        // =========================================================
        // Part 4: 武器位置與旋轉計算 (整合)
        // =========================================================
        
        // 1. 處理攻擊計時器邏輯
        if (intent.wantsAttack && _internalAttackTimer < 0) {
            CurrentState = PlayerState.Attacking;
            _internalAttackTimer = 0f;
        }

        if (_internalAttackTimer >= 0) {
            // --- 情況 A：正在進行物理攻擊軌跡 ---
            _internalAttackTimer += timeStep;
            float progress = Mathf.Clamp01(_internalAttackTimer / attackTime);
            
            SwordPose pose = GetAttackTrajectory(progress);
            intent.swordTargetLocalPos = pose.position;
            intent.swordTargetLocalRot = Quaternion.AngleAxis(pose.rotX, Vector3.right);
            CurrentState = PlayerState.Attacking;
            
            if (_internalAttackTimer >= attackTime)
            {
                CurrentState = PlayerState.Recovery;
                if(_internalAttackTimer >= attackTime + attackRecoveryTime)
                {
                    CurrentState = PlayerState.Waiting;
                    _internalAttackTimer = -1;
                }
            }
            else
            {
                // 攻擊時不移動
                intent.moveDirection = Vector3.zero;
            }
            
            // 攻擊時強制 wantsAttack 為 false，防止重複觸發
            intent.wantsAttack = false; 

        }
        else {
            // --- 情況 B：非攻擊狀態 ---
            if (mode == TrainingMode.Manual) {
                // 手動模式讀取鍵盤 (IJKL / UO / , .)
                var kb = Keyboard.current;
                float x = kb.lKey.isPressed ? 0.5f : (kb.jKey.isPressed ? -0.5f : 0f);
                float y = kb.uKey.isPressed ? 0.5f : (kb.oKey.isPressed ? -0.5f : 0f);
                float z = kb.iKey.isPressed ? 0.5f : (kb.kKey.isPressed ? -0.5f : 0f);
                intent.swordTargetLocalPos = new Vector3(x, y, z);

                float rx = kb.commaKey.isPressed ? 90f : (kb.periodKey.isPressed ? -90f : 0f);
                float rz = kb.nKey.isPressed ? 90f : (kb.mKey.isPressed ? -90f : 0f);
                intent.swordTargetLocalRot = Quaternion.Euler(rx, 0, rz);
            }
            else {
                // 自動模式預設位置
                intent.swordTargetLocalPos = Vector3.zero;
                intent.swordTargetLocalRot = Quaternion.identity;
            }
        }

        return intent;
    }

    public void ExecuteAgentAction(Vector3 moveDir, Vector3 lookDir, bool wantsDash, Vector3 swordOffsetPos, Quaternion swordTargetLocalRot)
    {
        if(_isDashing) 
            return;

        
        Vector3 clampedMove = Vector3.ClampMagnitude(moveDir, 1.0f);
        Vector3 localMove = transform.InverseTransformDirection(clampedMove);
        // for logger
        lastStrafeInput = Mathf.Abs(localMove.x);
        // 1. 執行身體移動 (原本的邏輯)
        ApplyMovement(moveDir);

        // 2. 執行身體旋轉
        Vector3 finalLookDir = (lookDir.sqrMagnitude > 0.001f) ? lookDir : moveDir;
        ApplyRotation(finalLookDir);

        // 3. 執行衝刺 (Discrete Action)
        if (wantsDash && _canDash && !_isDashing)
        {
            StartCoroutine(Dash(moveDir == Vector3.zero ? transform.forward : moveDir));
        }

        _currentSwordPos = swordOriPos + swordOffsetPos;
        _currentSwordRot = swordTargetLocalRot;

        // 5. 狀態處理
        if (CurrentState == PlayerState.Waiting)
        {
            ProcessWaitingState();
        }
    }

    private void ProcessWaitingState()
    {
        if(waitingTime < 0.2f)
        {
            waitingTime += Time.fixedDeltaTime;
        }
        if (waitingTime >= 0.2f && Random.value < waitingRecoveryChance)
        {
            CurrentState = PlayerState.Idle;
            waitingTime = 0;
        }
    }
    
    private Vector3 GetStrafeDirection(Vector3 directionToTarget)
    {
        _clockwise = (_clockwise != 0) ? _clockwise : (Random.value < 0.5f ? 1 : -1);
        return _clockwise * Vector3.Cross(Vector3.up, directionToTarget.normalized);
    }

    private void ApplyMovement(Vector3 direction)
    {
        if (CurrentState == PlayerState.Defending)
        {
            rb.velocity = Vector3.zero;
            return;
        }
        Vector3 controlledMove = Vector3.ClampMagnitude(direction, 1.0f);
        float currentSpeed = _isAiming ? aimingMoveSpeed : moveSpeed;

        rb.velocity = controlledMove * currentSpeed + new Vector3(0, rb.velocity.y, 0);
    }

    private void ApplyRotation(Vector3 direction)
    {
        direction.y = 0f; 

        if (direction.sqrMagnitude > 0.01f)
        {
            var targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
        }
    }
    private Vector3 GetMouseWorldPosition()
    {
        // 1. 從主攝像機創建一條射線，射向滑鼠在屏幕上的位置
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // 2. 創建一個與角色腳底等高的水平虛擬平面
        // Plane 的第一個參數是平面的法線方向（Y軸向上），第二個參數是平面上的一個點（角色的位置）
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        // 3. 計算射線與平面的交點
        if (groundPlane.Raycast(ray, out float distance))
        {
            // 如果射線擊中了平面，返回交點的坐標
            return ray.GetPoint(distance);
        }

        // 如果射線沒有擊中平面（例如，滑鼠指向天空），返回一個無效值
        return Vector3.zero; 
    }

    public struct SwordPose {
        public Vector3 position;
        public float rotX;
        public float rotZ;
    }

    // 這個函式就像 ApplyMovement，不具備狀態，只根據輸入的「時間點」給出「座標」
    public SwordPose GetAttackTrajectory(float normalizedTime)
    {
        SwordPose pose = new SwordPose();
        
        // 蓄力階段 (0% ~ 40%)
        if (normalizedTime < 0.4f) {
            float t = normalizedTime / 0.4f;
            pose.position = Vector3.zero;
            pose.rotX = Mathf.Lerp(0, -45f, t);
        }
        // 揮擊階段 (40% ~ 70%)
        else if (normalizedTime < 0.7f) {
            float t = (normalizedTime - 0.4f) / 0.3f;
            pose.position = Vector3.zero;
            pose.rotX = Mathf.Lerp(-45f, 90f, t);
        }
        // 收招階段 (70% ~ 100%)
        else {
            float t = (normalizedTime - 0.7f) / 0.3f;
            pose.position = Vector3.zero;
            pose.rotX = Mathf.Lerp(90f, 0f, t);
        }
        
        pose.rotZ = 0; // 暫時固定 Z 軸，或依需求增加
        return pose;
    }

    private IEnumerator LongAttack()
    {
        _isAtkDash = false; // ?
        CurrentState = PlayerState.Aiming;
        _actionProgress = 0f;
        rb.velocity = Vector3.zero;
        float time = 0f;
        Color targetColor = Color.yellow;
        _isAiming = true;
        Vector3 initialDirection = target.position - transform.position;
        initialDirection.y = 0f;
        transform.rotation = Quaternion.LookRotation(initialDirection.normalized);
        while (time < longAttackStartUpBasicTime - 1)
        {
            time += Time.fixedDeltaTime;
            float t = time / (longAttackStartUpBasicTime - 1) / 2f;
            _actionProgress = t;
            mat_player.color = Color.Lerp(Color.white, targetColor, t);
            Vector3 currentDirectionToTarget = target.position - transform.position;
            currentDirectionToTarget.y = 0f;

            float angleDifference = Vector3.Angle(transform.forward, currentDirectionToTarget);
            if (angleDifference > 5.0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(currentDirectionToTarget.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * aimingRotationSpeed);
            }
            yield return new WaitForFixedUpdate();
        }
        float bonusTime = Random.Range(0, longAttackStartUpBonusTime);
        int arrowDamage = longAttackDamage/2 + (int)(longAttackDamage/2 * bonusTime / longAttackStartUpBonusTime);
        float actualArrowSpeed = arrowSpeed/2 + arrowSpeed/2 * bonusTime / longAttackStartUpBonusTime;
        time = 0f;
        while (time < bonusTime)
        {
            time += Time.fixedDeltaTime;
            // maintain the t's increase rate as basic
            float t = Mathf.Min(time / (longAttackStartUpBasicTime - 1) / 2f + 0.5f, 1);
            _actionProgress = t;
            mat_player.color = Color.Lerp(Color.white, targetColor, t);
            Vector3 currentDirectionToTarget = target.position - transform.position;
            currentDirectionToTarget.y = 0f;

            float angleDifference = Vector3.Angle(transform.forward, currentDirectionToTarget);
            if (angleDifference > 5.0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(currentDirectionToTarget.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * aimingRotationSpeed);
            }
            yield return new WaitForFixedUpdate();
        }

        Vector3 spawnPos = transform.position + transform.forward;
        GameObject newArrow = Instantiate(arrowPrefab, spawnPos, transform.rotation, projectileParent);
        _activeProjectiles.Add(newArrow);
        var arrowComp = newArrow.GetComponent<Arrow>();
        if (arrowComp != null)
        {
            arrowComp.damage = arrowDamage;
            arrowComp.target = enemy;
            arrowComp.SetOwner(gameObject);
        }
        newArrow.GetComponent<Rigidbody>().velocity = transform.forward * actualArrowSpeed;

        CurrentState = PlayerState.Recovery;
        _actionProgress = 0f;
        time = 0f;
        targetColor = Color.white;
        _isAiming = false;
        while (time < longAttackRecoveryTime)
        {
            time += Time.fixedDeltaTime;
            float t = time / longAttackRecoveryTime;
            _actionProgress = t;
            mat_player.color = Color.Lerp(Color.yellow, targetColor, t);
            yield return new WaitForFixedUpdate();
        }
        _actionProgress = 0f;
        if (CurrentState == PlayerState.Recovery)
            CurrentState = PlayerState.Waiting;
    }

    private IEnumerator ManualLongAttack()
    {
        CurrentState = PlayerState.Aiming;
        _actionProgress = 0f;
        rb.velocity = Vector3.zero;
        float time = 0f;
        Color targetColor = Color.yellow;
        Color lineTargetColor = Color.red;
        _isAiming = true;
        while (time < longAttackStartUpBasicTime)
        {
            time += Time.fixedDeltaTime;
            float t = time / longAttackStartUpBasicTime;
            _actionProgress = t;
            mat_player.color = Color.Lerp(Color.white, targetColor, t);
            mat_line.color = Color.Lerp(Color.white, lineTargetColor, t);

            // shoot
            if (Input.GetMouseButtonUp(1))
            {
                break;
            }
            UpdateAimLine();

            yield return new WaitForFixedUpdate();
        }

        while (Input.GetMouseButton(1))
        {
            _actionProgress = 1;
            UpdateAimLine();

            yield return new WaitForFixedUpdate();
        }
        Vector3 spawnPos = transform.position + transform.forward;
        GameObject newArrow = Instantiate(arrowPrefab, spawnPos, transform.rotation, projectileParent);
        _activeProjectiles.Add(newArrow);
        var arrowComp = newArrow.GetComponent<Arrow>();
        if (arrowComp != null)
        {
            arrowComp.damage = longAttackDamage;
            arrowComp.target = enemy;
            arrowComp.SetOwner(gameObject);
        }
        newArrow.GetComponent<Rigidbody>().velocity = transform.forward * arrowSpeed * _actionProgress;

        CurrentState = PlayerState.Recovery;
        _actionProgress = 0f;
        time = 0f;
        targetColor = Color.white;
        _isAiming = false;
        if (aimLine != null && aimLine.enabled)
        {
            aimLine.enabled = false;
            mat_line.color = Color.white;
        }
        while (time < longAttackRecoveryTime)
        {
            time += Time.fixedDeltaTime;
            float t = time / longAttackRecoveryTime;
            _actionProgress = t;
            mat_player.color = Color.Lerp(Color.yellow, targetColor, t);
            yield return new WaitForFixedUpdate();
        }
        _actionProgress = 0f;
        if (CurrentState == PlayerState.Recovery)
            CurrentState = PlayerState.Waiting;
    }
    private void UpdateAimLine()
    {
        if (aimLine == null) return;

        if (!aimLine.enabled)
        {
            aimLine.enabled = true;
        }

        Vector3 rayStartPoint = transform.position + Vector3.up * 0.5f;
        float rayLength = 15.0f;

        Vector3 rayEndPoint = rayStartPoint + transform.forward * rayLength;

        aimLine.SetPosition(0, rayStartPoint);
        aimLine.SetPosition(1, rayEndPoint);
    }

    private IEnumerator Dash(Vector3 direction, bool isDirectAttack = false)
    {
        _isDashing = true;
        _canDash = false;
        Vector3 dashDir = direction.normalized;
        transform.forward = dashDir;
        if (dashDir == Vector3.zero && orientation != null) dashDir = orientation.forward;
        else if (dashDir == Vector3.zero) dashDir = transform.forward;

        rb.velocity = dashDir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);
        _isDashing = false;
        // if (isDirectAttack) StartCoroutine(Attack());
        rb.velocity = Vector3.zero;
        yield return new WaitForSeconds(dashCooldown);
        _canDash = true;
    }

    public void SetRandomTrainingMode()
    {
        var allModes = System.Enum.GetValues(typeof(TrainingMode));
        var availableModes = new List<TrainingMode>();
        if (isRandom)
        {
            foreach (TrainingMode mode in allModes)
            {
                // exclude escape, onlydash, SmartAttack, GAIL mode and any long ranged mode
                if (mode != TrainingMode.Manual && mode != TrainingMode.Random && mode != TrainingMode.Escape && mode != TrainingMode.OnlyDash && mode!= TrainingMode.SmartAttack && mode != TrainingMode.GAIL && mode != TrainingMode.LongRangeAttack && mode != TrainingMode.DashAtkRanged && mode != TrainingMode.AtkDashRanged)
                {
                    availableModes.Add(mode);
                }
            }
        }
        else if (isGail)
        {
            availableModes.Add(TrainingMode.DashAtkRanged);
            availableModes.Add(TrainingMode.AtkDashRanged);
        }
        
        curMode = availableModes[Random.Range(0, availableModes.Count)];
        if(printState)
            Debug.Log($"Player mode set to: {curMode}");
    }


    public void DeregisterProjectile(GameObject projectile)
    {
        if (_activeProjectiles.Contains(projectile))
        {
            _activeProjectiles.Remove(projectile);
        }
    }

    private IEnumerator AutoEquipWeapon(NearFarInteractor right_controller, XRGrabInteractable weapon, bool isManual)
    {
        yield return new WaitForSeconds(0.1f);
        XRInteractionManager manager = right_controller.interactionManager;

        // 強制讓手部進入「選中」狀態並抓取該武器
        // 這會觸發 XRI 內建的所有抓取邏輯（如自動對齊 Attach Point）
        manager.SelectEnter((IXRSelectInteractor)right_controller, (IXRSelectInteractable)weapon);

        if (!isManual)
        {
            handPoseDriver.enabled = false;
        }
    }
    private int GetDecisionPeriod()
    {
        var requester = GetComponent<Unity.MLAgents.DecisionRequester>();
        return (requester != null) ? requester.DecisionPeriod : 1;
    }

    // 簡化版的紀錄邏輯
    // 在 VR_Player (繼承類) 的 FixedUpdate 或 VR_Player_Behavior 中呼叫
    protected void PerformDataLogging()
    {
        if (!isLogging || enemy == null || activateSword == null) return;

        // 1. 取得狀態：距離
        float dist = Vector3.Distance(transform.position, enemy.transform.position);

        // 2. 取得動作：側移強度 (已在 ExecuteAgentAction 存入 lastStrafeInput)
        float strafe = lastStrafeInput;

        // 3. 取得動作：角速度 (弧度)
        VR_Sword sword = sword_object.GetComponent<VR_Sword>();
        float angVel = sword != null ? sword.GetAngularSpeedRad() : 0f;

        // 4. 寫入 CSV (Label, Dist, Strafe, AngVel)
        string label = IsUseInternalLogic ? "Human" : "AI";
        _logCache.AppendLine($"{label},{dist:F3},{strafe:F3},{angVel:F3}");

        // 當快取超過一定長度再寫入，或在 Episode 結束時寫入
        if (_logCache.Length > 1024)
        {
            System.IO.File.AppendAllText(csvPath, _logCache.ToString());
            _logCache.Clear();
        }
    }
}
