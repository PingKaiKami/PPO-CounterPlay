using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HP))]
public class Enemy_Dodge_Agent : Agent
{
    [Header("Movement Settings")]
    public Transform enemyObj;
    public Transform orientation; 
    public Transform playerTransform;
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;

    private HP enemy_HP;
    private Vector3 moveDirection;
    public override void Initialize()
    {
        base.OnEpisodeBegin(); // 确保呼叫 base 方法
        if (enemyObj == null) { enemyObj = transform; }
    }
    void Start()
    {
        enemy_HP = GetComponent<HP>();
    }

    public override void OnEpisodeBegin()
    {
        StopAllCoroutines();
        Vector3 newPos;
        do
        {
            newPos = new Vector3(Random.Range(-5, 5), 0, Random.Range(-5, 5));
        } while (newPos == Vector3.zero);
        transform.localPosition = newPos;

        playerTransform.localPosition = new Vector3(0, 0.5f, 0);
        if (enemy_HP != null) { enemy_HP.ResetHealth(); }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(moveDirection.normalized);
        sensor.AddObservation(enemyObj.forward);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int moveAction = actions.DiscreteActions[0];
        UpdateMoveDirection(moveAction);
        float patrolZoneLimit = 15.0f;

        if (Mathf.Abs(transform.localPosition.x) > patrolZoneLimit || 
            Mathf.Abs(transform.localPosition.z) > patrolZoneLimit)
        {
            AddReward(-0.1f);
        }
        
        AddReward(0.001f);
    }
    
    public void OnDamageTaken(int damageTaken)
    {
        // 被命中是一个明确的负面事件，给予一个显著的惩罚。
        AddReward(-0.2f);
    }
    public void OnPlayerLongAttackMissed()
    {
        // AddReward(0.2f);
    }

    void FixedUpdate()
    {
        if (enemy_HP != null && enemy_HP.IsDead())
        {
            SetReward(-1.0f);
            EndEpisode();
            return;
        }

        if (moveDirection != Vector3.zero)
        {
            transform.Translate(moveDirection.normalized * moveSpeed * Time.fixedDeltaTime, Space.World);
        }
    }
    /// <summary>
    /// 根据动作指令和 orientation 计算移动方向，并处理旋转。
    /// </summary>
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

        // 旋转逻辑也在这里，与 Enemy_Agent 保持一致
        if (moveDirection != Vector3.zero)
        {
            enemyObj.forward = Vector3.Slerp(enemyObj.forward, moveDirection.normalized, Time.deltaTime * rotationSpeed);
        }
    }
    
    // 手动测试模式
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions.Clear();

        int moveAction = 0;
        if (Input.GetKey(KeyCode.UpArrow)) moveAction = 1;
        if (Input.GetKey(KeyCode.DownArrow)) moveAction = 2;
        if (Input.GetKey(KeyCode.LeftArrow)) moveAction = 3;
        if (Input.GetKey(KeyCode.RightArrow)) moveAction = 4;
        discreteActions[0] = moveAction;
    }
}
