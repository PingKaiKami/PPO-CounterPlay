using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Behavior : MonoBehaviour
{
    public enum PlayerState
    {
        Idle,
        StartUp,
        Recovery,
        Defending,
        Waiting
    }

    public enum TrainingMode
    {
        AutoTrace,
        OnlyAttack,
        OnlyDash,
        AttackAndRun,
        AttackAndDash,
        SmartAttack,
        Escape,
        Random,
        Manual
    }

    public GameObject enemy;

    [Header("Behavior Settings")]
    public PlayerState curState;
    public TrainingMode curMode;
    public Transform target;
    public Transform orientation;

    [Header("Movement & Dash")]
    public float moveSpeed = 3f;
    public float dashSpeed = 30f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    [Header("Attack")]
    public int damage = 10;
    public float attackBufferRange = 0.2f; // 預輸入距離(因為攻擊有延遲)
    public float attackStartUpTime = 0.5f;
    public float attackCooldown = 1f;

    [Header("AI Decision Making")]
    [Tooltip("左右橫移的速度")]
    public float strafeSpeed = 1.5f;
    [Tooltip("每隔多少秒改變一次橫移方向")]
    public float strafeDirectionChangeInterval = 2.0f;
    public float waitingPossibility = 0.9f;
    protected Rigidbody rb;
    protected AttackRange attackRange;
    protected float attackRadius;
    protected Material mat;
    private float distanceToEnemy = 3; // 保持與敵人的距離
    private Vector3 moveDirection;
    private int clockwise = 0; // AttackAndRun & AttackAndDash 模式的內部狀態
    private bool isDashing = false;
    private bool canDash = true;
    private float waitTime = 0;
    private int randomWaitTime = 0;
    private bool forceSmartAttack = false;
    protected private void Move()
    {
        Vector3 directionToTarget;
        float distanceToTarget;

        switch (curMode)
        {
            case TrainingMode.Manual:
                float horizontalInput = 0f;
                float verticalInput = 0f;

                if (Input.GetKey(KeyCode.W)) verticalInput += 1f;
                if (Input.GetKey(KeyCode.S)) verticalInput -= 1f;
                if (Input.GetKey(KeyCode.D)) horizontalInput += 1f;
                if (Input.GetKey(KeyCode.A)) horizontalInput -= 1f;

                moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;
                moveDirection.y = 0f;

                if (!isDashing)
                {
                    rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                }

                if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
                {
                    StartCoroutine(Dash());
                }
                if (Input.GetMouseButtonDown(0) && curState == PlayerState.Idle)
                {
                    StartCoroutine(Attack());
                }
                if (Input.GetMouseButton(1) && curState == PlayerState.Idle || curState == PlayerState.Defending)
                {
                    curState = PlayerState.Defending;
                    mat.color = Color.blue;
                }
                if (Input.GetMouseButtonUp(1) && curState == PlayerState.Defending && (curState != PlayerState.StartUp || curState != PlayerState.Recovery))
                {
                    curState = PlayerState.Idle;
                    mat.color = Color.white;
                }
                break;

            case TrainingMode.AutoTrace:
                moveDirection = target.position - transform.position;
                moveDirection.y = 0f;

                if (!isDashing)
                {
                    rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                }

                if (moveDirection.magnitude <= (attackRadius + attackBufferRange) && curState == PlayerState.Idle)
                {
                    StartCoroutine(Attack());
                }
                break;
            case TrainingMode.OnlyAttack:
                moveDirection = target.position - transform.position;
                moveDirection.y = 0f;

                if (moveDirection.magnitude <= attackRadius && curState == PlayerState.Idle)
                {
                    StartCoroutine(Attack());
                }
                break;

            case TrainingMode.OnlyDash:
                directionToTarget = target.position - transform.position;
                directionToTarget.y = 0;
                distanceToTarget = directionToTarget.magnitude;
                if (canDash)
                {
                    if (distanceToTarget > distanceToEnemy)
                    {
                        moveDirection = directionToTarget;
                        StartCoroutine(Dash());
                    }
                    else
                    {
                        moveDirection = new Vector3((Random.value < 0.5f ? -1 : 1) * directionToTarget.z, 0, directionToTarget.x);
                        StartCoroutine(Dash());
                    }
                }
                break;

            case TrainingMode.AttackAndRun:
                directionToTarget = target.position - transform.position;
                directionToTarget.y = 0;
                distanceToTarget = directionToTarget.magnitude;
                if (curState == PlayerState.Idle || curState == PlayerState.StartUp)
                {
                    if (distanceToTarget > (attackRadius + attackBufferRange) || curState == PlayerState.StartUp)
                    {
                        moveDirection = directionToTarget;
                        rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                    }
                    else
                    {
                        StartCoroutine(Attack());
                    }
                    clockwise = 0;
                }
                else
                {
                    if (distanceToTarget < distanceToEnemy)
                    {
                        moveDirection = -directionToTarget;
                        rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                    }
                    else
                    {
                        clockwise = (clockwise != 0) ? clockwise : Random.value < 0.5f ? 1 : -1;
                        Vector3 strafeDirection = clockwise * Vector3.Cross(Vector3.up, directionToTarget.normalized);
                        rb.velocity = strafeDirection * moveSpeed * 0.8f + new Vector3(0, rb.velocity.y, 0);
                    }
                }
                break;

            case TrainingMode.AttackAndDash:
                if (target == null) return;
                directionToTarget = target.position - transform.position;
                directionToTarget.y = 0;
                distanceToTarget = directionToTarget.magnitude;

                if (curState == PlayerState.Idle || curState == PlayerState.StartUp)
                {
                    if (distanceToTarget > (attackRadius + attackBufferRange) || curState == PlayerState.StartUp)
                    {
                        moveDirection = directionToTarget;
                        rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                    }
                    else
                    {
                        StartCoroutine(Attack());
                    }
                }
                else
                {
                    if (curState != PlayerState.Waiting && canDash)
                    {
                        moveDirection = -directionToTarget;
                        StartCoroutine(Dash());
                        clockwise = 0;
                    }
                    else if (!isDashing)
                    {
                        if (distanceToTarget < distanceToEnemy)
                        {
                            moveDirection = -directionToTarget;
                            rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                        }
                        else
                        {
                            clockwise = (clockwise != 0) ? clockwise : Random.value < 0.5f ? 1 : -1;
                            Vector3 strafeDirection = clockwise * Vector3.Cross(Vector3.up, directionToTarget.normalized);
                            rb.velocity = strafeDirection * moveSpeed * 0.8f + new Vector3(0, rb.velocity.y, 0);
                        }
                    }

                }
                break;

            case TrainingMode.SmartAttack:
                if (target == null) return;
                directionToTarget = target.position - transform.position;
                directionToTarget.y = 0;
                distanceToTarget = directionToTarget.magnitude;

                if (curState == PlayerState.Idle || curState == PlayerState.StartUp || forceSmartAttack)
                {
                    if (enemy.GetComponent<Enemy_Agent>().GetEnemyState() == Enemy_Agent.EnemyState.Recovery || curState == PlayerState.StartUp || forceSmartAttack)
                    {
                        if (distanceToTarget > (attackRadius + attackBufferRange) || curState == PlayerState.StartUp)
                        {
                            moveDirection = directionToTarget;
                            rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                        }
                        else
                        {
                            StartCoroutine(Attack());
                            forceSmartAttack = false;
                        }
                    }
                    else
                    {
                        if (distanceToTarget < distanceToEnemy)
                        {
                            moveDirection = -directionToTarget;
                            rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                        }
                        else
                        {
                            clockwise = (clockwise != 0) ? clockwise : Random.value < 0.5f ? 1 : -1;
                            Vector3 strafeDirection = clockwise * Vector3.Cross(Vector3.up, directionToTarget.normalized);
                            rb.velocity = strafeDirection * moveSpeed * 0.8f + new Vector3(0, rb.velocity.y, 0);
                        }

                        if (waitTime < randomWaitTime)
                        {
                            waitTime += Time.deltaTime;
                        }
                        else
                        {
                            forceSmartAttack = true;
                            waitTime = 0f;
                            SetRandomTime();
                        }

                    }
                }
                else
                {
                    if (curState != PlayerState.Waiting && canDash)
                    {
                        moveDirection = -directionToTarget;
                        StartCoroutine(Dash());
                        clockwise = 0;
                    }
                    else if (!isDashing)
                    {
                        if (distanceToTarget < distanceToEnemy)
                        {
                            moveDirection = -directionToTarget;
                            rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                        }
                        else
                        {
                            clockwise = (clockwise != 0) ? clockwise : Random.value < 0.5f ? 1 : -1;
                            Vector3 strafeDirection = clockwise * Vector3.Cross(Vector3.up, directionToTarget.normalized);
                            rb.velocity = strafeDirection * moveSpeed * 0.8f + new Vector3(0, rb.velocity.y, 0);
                        }
                    }
                }
                break;

            case TrainingMode.Escape:
                moveDirection = transform.position - target.position;
                moveDirection.y = 0f;

                if (!isDashing)
                {
                    rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                }
                break;
        }

    }
    protected private void Wait()
    {
        if (Random.value > waitingPossibility)
        {
            curState = PlayerState.Idle;
        }
    }
    IEnumerator Attack()
    {
        curState = PlayerState.StartUp;
        float time = 0f;
        Color targetColor = Color.yellow;
        while (time < attackStartUpTime)
        {
            time += Time.deltaTime;
            float t = time / attackStartUpTime;
            mat.color = Color.Lerp(Color.white, targetColor, t);
            yield return null;
        }

        bool isInRange = attackRange.IsEnemyInRange();
        if (isInRange)
        {
            GameObject[] enemies = attackRange.GetEnemyInRange();
            foreach (GameObject enemy in enemies)
            {
                HP enemy_HP = enemy.GetComponentInParent<HP>();
                Enemy_Agent enemy_Agent = enemy.GetComponentInParent<Enemy_Agent>();
                if (enemy_Agent.curState == Enemy_Agent.EnemyState.Defending)
                {
                    enemy_HP.Hurt(damage / 10);
                }
                else
                {
                    enemy_HP.Hurt(damage);
                }

            }
        }
        else
        {
            enemy.GetComponent<Enemy_Agent>().OnPlayerAttackMissed();
        }

        curState = PlayerState.Recovery;
        time = 0f;
        targetColor = Color.white;
        while (time < attackCooldown)
        {
            time += Time.deltaTime;
            float t = time / attackCooldown;
            mat.color = Color.Lerp(Color.yellow, targetColor, t);
            yield return null;
        }
        if (curState == PlayerState.Recovery)
            curState = PlayerState.Waiting;
    }
    IEnumerator Dash()
    {
        isDashing = true;
        canDash = false;
        Vector3 dashDir = moveDirection.normalized;
        if (dashDir == Vector3.zero)
            dashDir = orientation.forward;

        rb.velocity = dashDir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        isDashing = false;
        rb.velocity = Vector3.zero;
        yield return new WaitForSeconds(dashCooldown);

        canDash = true;
    }
    public PlayerState GetCurrentState()
    {
        return curState;
    }
    public bool IsEnemyInAttackRange(GameObject enemy)
    {
        if (attackRange != null)
        {
            return attackRange.IsSpecificEnemyInRange(enemy);
        }
        return false;
    }
    public void SetRandomTrainingMode()
    {
        System.Array allModes = System.Enum.GetValues(typeof(TrainingMode));

        List<TrainingMode> availableModes = new List<TrainingMode>();
        foreach (TrainingMode mode in allModes)
        {
            // 暫時排除 Escape, OnlyAttack, OnlyDash的模式
            if (mode != TrainingMode.Manual && mode != TrainingMode.Random && mode != TrainingMode.Escape && mode != TrainingMode.OnlyAttack && mode != TrainingMode.OnlyDash)
            {
                availableModes.Add(mode);
            }
        }
        int randomIndex = Random.Range(0, availableModes.Count);
        curMode = availableModes[randomIndex];

        Debug.Log($"Player mode set to: {curMode}");
    }
    protected private void SetRandomTime()
    {
        randomWaitTime = Random.Range(3, 5);
    }
    public void ChangeDistanceToEnemy(float distance)
    {
        distanceToEnemy = distance;
    }
}
