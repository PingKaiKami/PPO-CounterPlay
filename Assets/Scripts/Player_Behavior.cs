using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class Player_Behavior : MonoBehaviour
{
    public enum PlayerState { Idle, StartUp, Recovery, Defending, Waiting, Dashing }
    public enum TrainingMode { AutoTrace, OnlyDash, AttackAndRun, AttackAndDash, SmartAttack, LongRangeAttack, DashAtkRanged, AtkDashRanged, Escape, Random, Manual }

    [Header("References")]
    public GameObject enemy;
    public Transform target;
    public Transform orientation;

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
    public float attackBufferRange = 0.5f;
    public float attackStartUpTime = 0.5f;
    public float attackRecoveryTime = 1f;

    [Header("LongAttack")]
    public GameObject arrowPrefab;
    public float aimingRotationSpeed;
    public int longAttackDamage = 20;
    public float longAttackStartUpTime = 2f;
    public float longAttackRecoveryTime = 1f;
    public float longAttackRange = 5f;
    public float arrowSpeed = 1f;

    [Header("AI Decision Making")]
    public float strafeSpeed = 1.5f;

    [Range(0.01f, 1.0f)]
    public float waitingRecoveryChance = 0.1f;

    [Header("Debug")]
    public PlayerState CurrentState = PlayerState.Idle;

    public bool printState = false;

    protected Rigidbody rb;
    protected AttackRange attackRange;
    protected float attackRadius;
    protected Material mat;

    private Vector3 _moveDirection;
    private bool _isDashing = false;
    private bool _canDash = true;
    private bool _isAiming = false;
    private int _clockwise = 0;
    private bool _forceAtkDashRanged = false;
    private List<GameObject> _activeProjectiles = new List<GameObject>();
    public IReadOnlyList<GameObject> ActiveProjectiles => _activeProjectiles;
    private float _actionProgress = 0f;
    private TrainingArea myArea;
    private Transform projectileParent;
    public PlayerState GetCurrentState() => CurrentState;
    public bool IsAiming() => _isAiming;
    public float GetActionProgress() => _actionProgress;
    public bool IsEnemyInAttackRange(GameObject e) => attackRange != null && attackRange.IsSpecificEnemyInRange(e);

    protected void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        attackRange = GetComponentInChildren<AttackRange>();
        if (attackRange != null) attackRadius = attackRange.radius;
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null) mat = renderer.material;
        myArea = GetComponentInParent<TrainingArea>();
        if (myArea != null)
        {
            projectileParent = myArea.transform.Find("Projectile_Parent");
            if (projectileParent == null)
            {
                projectileParent = new GameObject("Projectile_Parent").transform;
                projectileParent.SetParent(myArea.transform);
            }
        }
    }

    protected void UpdateBehavior()
    {
        if (CurrentState == PlayerState.StartUp || _isDashing)
        {
            return;
        }

        _moveDirection = CalculateMoveDirection();
        ApplyMovement(_moveDirection);
        ApplyRotation(_moveDirection);

        if (CurrentState == PlayerState.Waiting)
        {
            ProcessWaitingState();
            return;
        }
        TriggerActions(_moveDirection);
    }
    private void ProcessWaitingState()
    {
        if (Random.value < waitingRecoveryChance * Time.fixedDeltaTime)
        {
            CurrentState = PlayerState.Idle;
        }
    }

    private Vector3 CalculateMoveDirection()
    {
        if (target == null && curMode != TrainingMode.Manual) return Vector3.zero;

        Vector3 directionToTarget = target.position - transform.position;
        directionToTarget.y = 0;
        float distanceToTarget = directionToTarget.magnitude;

        switch (curMode)
        {
            case TrainingMode.Manual:
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                return (orientation.forward * v + orientation.right * h).normalized;

            case TrainingMode.AutoTrace:
                return directionToTarget;

            case TrainingMode.AttackAndRun:
            case TrainingMode.AttackAndDash:
                if (CurrentState == PlayerState.Idle)
                    return (distanceToTarget > (attackRadius + attackBufferRange)) ? directionToTarget : Vector3.zero;
                else
                    return (distanceToTarget < 3f) ? -directionToTarget : GetStrafeDirection(directionToTarget);

            case TrainingMode.SmartAttack:
                var enemyAgent = enemy.GetComponent<Enemy_Agent>();
                if (enemyAgent.GetEnemyState() == Enemy_Agent.EnemyState.Recovery)
                    return (distanceToTarget > (attackRadius + attackBufferRange)) ? directionToTarget : Vector3.zero;
                else
                    return GetDefensiveMoveDirection(distanceToTarget, directionToTarget);

            case TrainingMode.LongRangeAttack:
                return (distanceToTarget < longAttackRange) ? -directionToTarget : (distanceToTarget > longAttackRange * 1.5f) ? directionToTarget : GetStrafeDirection(directionToTarget);

            case TrainingMode.DashAtkRanged:
                return (distanceToTarget < longAttackRange) ? -directionToTarget : (distanceToTarget > longAttackRange * 1.5f) ? directionToTarget : GetStrafeDirection(directionToTarget);

            case TrainingMode.AtkDashRanged:
                if (_forceAtkDashRanged)
                {
                    return (distanceToTarget > (attackRadius + attackBufferRange)) ? directionToTarget : Vector3.zero;
                }
                else {
                    return (distanceToTarget < longAttackRange) ? -directionToTarget : (distanceToTarget > longAttackRange * 1.5f) ? directionToTarget : GetStrafeDirection(directionToTarget);
                }

            case TrainingMode.Escape:
                return -directionToTarget;

            default:
                return Vector3.zero;
        }
    }

    private Vector3 GetDefensiveMoveDirection(float distance, Vector3 direction)
    {
        return distance < 3f ? -direction : GetStrafeDirection(direction);
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
        rb.velocity = direction.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
    }

    private void ApplyRotation(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
        {
            var targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);
        }
    }

    private void TriggerActions(Vector3 moveDir)
    {
        if (CurrentState != PlayerState.Idle && CurrentState != PlayerState.Recovery && CurrentState != PlayerState.Waiting) return;

        if (curMode == TrainingMode.Manual)
        {
            if (Input.GetMouseButtonDown(0)) StartCoroutine(Attack());
            if (Input.GetKeyDown(KeyCode.LeftShift) && _canDash) StartCoroutine(Dash(moveDir));
            // Defending logic...
        }
        else // AI Trigger Logic
        {
            Vector3 directionToTarget = target.position - transform.position;
            directionToTarget.y = 0;
            float distanceToTarget = directionToTarget.magnitude;

            if (curMode == TrainingMode.AutoTrace)
            {
                if (CurrentState == PlayerState.Idle)
                {
                    if (distanceToTarget <= (attackRadius + attackBufferRange)) StartCoroutine(Attack());
                }

            }
            else if (curMode == TrainingMode.AttackAndRun)
            {
                if (CurrentState == PlayerState.Idle)
                {
                    if (distanceToTarget <= (attackRadius + attackBufferRange)) StartCoroutine(Attack());
                }

            }
            else if (curMode == TrainingMode.AttackAndDash)
            {
                if (CurrentState == PlayerState.Idle)
                {
                    if (distanceToTarget <= (attackRadius + attackBufferRange))
                    {
                        StartCoroutine(Attack());
                    }
                }
                else
                {
                    if (_canDash)
                    {
                        StartCoroutine(Dash(-directionToTarget));
                    }
                }
            }
            else if (curMode == TrainingMode.SmartAttack)
            {
                var enemyAgent = enemy.GetComponent<Enemy_Agent>();
                if (CurrentState == PlayerState.Idle && enemyAgent.GetEnemyState() == Enemy_Agent.EnemyState.Recovery)
                {
                    if (distanceToTarget <= (attackRadius + attackBufferRange))
                    {
                        StartCoroutine(Attack());
                    }
                    else
                    {
                        if (_canDash)
                        {
                            StartCoroutine(Dash(directionToTarget));
                        }
                    }
                }
                else
                {
                    if (distanceToTarget <= (attackRadius + attackBufferRange) && _canDash)
                    {
                        StartCoroutine(Dash(-directionToTarget));
                    }
                }
            }
            else if (curMode == TrainingMode.LongRangeAttack)
            {
                if (CurrentState == PlayerState.Idle)
                {
                    if (distanceToTarget >= longAttackRange) StartCoroutine(LongAttack());
                }

            }
            else if (curMode == TrainingMode.DashAtkRanged)
            {
                if (CurrentState == PlayerState.Idle)
                {
                    if (distanceToTarget >= longAttackRange)
                    {
                        if (Random.value > 0.5f && !_isDashing)
                        {
                            StartCoroutine(LongAttack());
                        }
                        else
                        {
                            if (_canDash)
                            {
                                StartCoroutine(Dash(directionToTarget, isDirectAttack: true));
                            }
                        }
                    }
                }

            }
            else if (curMode == TrainingMode.AtkDashRanged)
            {
                if (CurrentState == PlayerState.Idle)
                {
                    if (distanceToTarget >= longAttackRange)
                    {
                        if (Random.value > 0.5f && !_forceAtkDashRanged)
                        {
                            StartCoroutine(LongAttack());
                        }
                        else
                        {
                            _forceAtkDashRanged = true;
                        }
                    }
                    else
                    {
                        if (distanceToTarget <= (attackRadius + attackBufferRange))
                        {
                            StartCoroutine(Attack());
                        }
                    }
                }
                else
                {
                    if (_forceAtkDashRanged && _canDash)
                    {
                        StartCoroutine(Dash(-directionToTarget));
                        _forceAtkDashRanged = false;
                    }
                }
                
            }
            else if (curMode == TrainingMode.OnlyDash && _canDash)
            {
                StartCoroutine(Dash(moveDir));
            }
        }
    }

    private IEnumerator Attack()
    {
        CurrentState = PlayerState.StartUp;
        _actionProgress = 0f;
        rb.velocity = Vector3.zero;
        float time = 0f;
        Color targetColor = Color.yellow;
        while (time < attackStartUpTime)
        {
            time += Time.fixedDeltaTime;
            float t = time / attackStartUpTime;
            _actionProgress = t;
            mat.color = Color.Lerp(Color.white, targetColor, t);
            yield return new WaitForFixedUpdate();
        }

        if (attackRange.IsEnemyInRange())
        {
            var enemies = attackRange.GetEnemyInRange();
            foreach (var e in enemies)
            {
                var enemyHP = e.GetComponentInParent<HP>();
                if (enemyHP != null) enemyHP.HurtFromMelee(damage);
            }
        }
        else
        {
            enemy?.GetComponent<Enemy_Agent>()?.OnPlayerAttackMissed();
        }
        
        CurrentState = PlayerState.Recovery;
        _actionProgress = 0f;
        time = 0f;
        targetColor = Color.white;
        while (time < attackRecoveryTime)
        {
            time += Time.fixedDeltaTime;
            float t = time / attackRecoveryTime;
            _actionProgress = t;
            mat.color = Color.Lerp(Color.yellow, targetColor, t);
            yield return new WaitForFixedUpdate();
        }
        CurrentState = PlayerState.Waiting;
    }

    private IEnumerator LongAttack()
    {
        CurrentState = PlayerState.StartUp;
        _actionProgress = 0f;
        rb.velocity = Vector3.zero;
        float time = 0f;
        Color targetColor = Color.yellow;
        _isAiming = true;
        Vector3 initialDirection = target.position - transform.position;
        initialDirection.y = 0f;
        transform.rotation = Quaternion.LookRotation(initialDirection.normalized);

        while (time < longAttackStartUpTime)
        {
            time += Time.fixedDeltaTime;
            float t = time / longAttackStartUpTime;
            _actionProgress = t;
            mat.color = Color.Lerp(Color.white, targetColor, t);
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
            arrowComp.damage = longAttackDamage;
            arrowComp.target = enemy;
            arrowComp.SetOwner(gameObject);
        }
        newArrow.GetComponent<Rigidbody>().velocity = transform.forward * arrowSpeed;

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
            mat.color = Color.Lerp(Color.yellow, targetColor, t);
            yield return new WaitForFixedUpdate();
        }
        _actionProgress = 0f;
        if (CurrentState == PlayerState.Recovery)
            CurrentState = PlayerState.Waiting;
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
        if (isDirectAttack) StartCoroutine(Attack());
        rb.velocity = Vector3.zero;
        yield return new WaitForSeconds(dashCooldown);
        _canDash = true;
    }

    public void SetRandomTrainingMode()
    {
        var allModes = System.Enum.GetValues(typeof(TrainingMode));
        var availableModes = new List<TrainingMode>();
        foreach (TrainingMode mode in allModes)
        {
            if (mode != TrainingMode.Manual && mode != TrainingMode.Random && mode != TrainingMode.Escape)
            {
                availableModes.Add(mode);
            }
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
}