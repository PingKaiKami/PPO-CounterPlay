using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HP))]
public class Enemy_Dodge_Agent : Agent
{
    public enum EnemyState
    {
        Idle,
        StartUp,
        Recovery,
        Defending
    }
    public EnemyState curState = EnemyState.Idle;
    [Header("Movement Settings")]
    public Transform enemyObj;
    public Transform orientation; 
    public Transform playerTransform;
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;

    private Player_Behavior_Dodge pbd;
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
        pbd = playerTransform.GetComponent<Player_Behavior_Dodge>();
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
        // 1. 自己的面對方向 (3 個值)
        sensor.AddObservation(enemyObj.forward.normalized);

        // 2. 自己到玩家的方向向量 (3 個值)
        Vector3 toPlayer = playerTransform.position - transform.position;
        sensor.AddObservation(toPlayer.normalized);

        // 3. 自己是否正對著玩家 (1 個值)
        float alignment = Vector3.Dot(enemyObj.forward.normalized, toPlayer.normalized);
        sensor.AddObservation(alignment);

        // 4. 自己到玩家的距離(1 個值)
        sensor.AddObservation(toPlayer.magnitude);

        // 5. 玩家速度向量 (3 個值)
        // 確保 rigidbody 不是 null
        Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
        sensor.AddObservation(playerRb != null ? playerRb.velocity : Vector3.zero);

        // 6. 玩家面對方向 (3 個值)
        sensor.AddObservation(playerTransform.forward.normalized);

        // 7. 玩家的詳細狀態 (PlayerState) (1 個值)
        // 將 enum 轉換為整數，讓 AI 學習
        sensor.AddObservation((int)pbd.GetCurrentState());
        
        // 8. 自己的狀態 (EnemyState) (1 個值)
        sensor.AddObservation((int)curState);
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
    
    public void OnDamageTakenFromMelee(int damageTaken)
    {
        AddReward(-0.2f);
    }
    public void OnDamageTakenFromRanged(int damageTaken)
    {
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

        if (playerTransform.position.y < -5)
        {
            SetReward(0);
            EndEpisode();
            return;
        }

        if (moveDirection != Vector3.zero)
        {
            transform.Translate(moveDirection.normalized * moveSpeed * Time.fixedDeltaTime, Space.World);
        }
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
            enemyObj.forward = Vector3.Slerp(enemyObj.forward, moveDirection.normalized, Time.fixedDeltaTime * rotationSpeed);
        }
    }
    
    // 手動測試模式
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
