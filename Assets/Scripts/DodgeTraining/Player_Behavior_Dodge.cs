using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Behavior_Dodge : MonoBehaviour
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
        LongRangeAttack,
        Manual
    }
    public GameObject enemy;

    [Header("Behavior Settings")]
    public PlayerState curState;
    public TrainingMode curMode;
    public Transform target;
    public Transform orientation;

    [Header("Movement & Dash")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float dashSpeed = 30f;
    public float dashDuration = 0.1f;
    public float dashCooldown = 1f;

    [Header("Attack")]
    public int damage = 15;
    public float attackBufferRange = 0.5f; // 預輸入距離(因為攻擊有延遲)
    public float attackStartUpTime = 0.5f;
    public float attackRecoveryTime = 1f;

    [Header("LongAttack")]
    public GameObject arrow;
    public int longAttackDamage = 20;
    public float longAttackStartUpTime = 3f;
    public float longAttackRecoveryTime = 1f;
    public float longAttackRange = 5f;
    public float arrowSpeed = 1f;

    [Header("AI Decision Making")]
    [Tooltip("左右橫移的速度")]
    public float strafeSpeed = 1.5f;
    [Tooltip("每隔多少秒改變一次橫移方向")]
    public float strafeDirectionChangeInterval = 2.0f;
    public float waitingPossibility = 0.99f;
    protected Rigidbody rb;
    protected AttackRange attackRange;
    protected float attackRadius;
    protected Material mat;
    private Vector3 moveDirection;
    private int clockwise = 0; // AttackAndRun & AttackAndDash 模式的內部狀態
    private bool isDashing = false;
    private bool canDash = true;
    private bool isAimed;
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

            case TrainingMode.LongRangeAttack:
                directionToTarget = target.position - transform.position;
                directionToTarget.y = 0;
                distanceToTarget = directionToTarget.magnitude;
                if (curState == PlayerState.Idle || curState == PlayerState.Waiting)
                {
                    if (distanceToTarget < longAttackRange)
                    {
                        moveDirection = -directionToTarget;
                        rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                        Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                    }
                    else if (distanceToTarget > longAttackRange * 2)
                    {
                        moveDirection = directionToTarget;
                        rb.velocity = moveDirection.normalized * moveSpeed + new Vector3(0, rb.velocity.y, 0);
                        Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                    }
                    else
                    {
                        clockwise = (clockwise != 0) ? clockwise : Random.value < 0.5f ? 1 : -1;
                        Vector3 strafeDirection = clockwise * Vector3.Cross(Vector3.up, directionToTarget.normalized);
                        rb.velocity = strafeDirection * moveSpeed * 0.8f + new Vector3(0, rb.velocity.y, 0);
                        Quaternion targetRotation = Quaternion.LookRotation(strafeDirection.normalized);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                        if (curState == PlayerState.Idle)
                        {
                            StartCoroutine(LongAttack());
                        }
                    }
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
    IEnumerator LongAttack()
    {
        curState = PlayerState.StartUp;
        rb.velocity = Vector3.zero;
        float time = 0f;
        Color targetColor = Color.yellow;

        while (time < longAttackStartUpTime - 1)
        {
            time += Time.deltaTime;
            float t = time / longAttackStartUpTime;
            mat.color = Color.Lerp(Color.white, targetColor, t);
            moveDirection = target.position - transform.position;
            moveDirection.y = 0f;
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            yield return null;
        }
        isAimed = false;
        while (time < longAttackStartUpTime)
        {
            if (!isAimed)
            {
                moveDirection = target.position - transform.position;
                moveDirection.y = 0f;
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                isAimed = true;
            }
            time += Time.deltaTime;
            yield return null;
        }

        GameObject newArrow = Instantiate(arrow, transform.position + moveDirection.normalized, arrow.transform.rotation);
        newArrow.GetComponent<Arrow_Dodge>().damage = longAttackDamage;

        Rigidbody arrow_rb = newArrow.GetComponent<Rigidbody>();
        arrow_rb.velocity = moveDirection * arrowSpeed;

        curState = PlayerState.Recovery;
        time = 0f;
        targetColor = Color.white;
        while (time < longAttackRecoveryTime)
        {
            time += Time.deltaTime;
            float t = time / longAttackRecoveryTime;
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
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

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
}
