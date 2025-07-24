using UnityEngine;
using System.Collections;
public class Enemy_Behavior : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        StartUp,
        Attacking,
        Recovery,
        Defending
    }
    public EnemyState curState = EnemyState.Idle;
    public Transform orientation;
    public Transform enemyObj;
    public float moveSpeed = 3f;
    public float rotationSpeed = 3f;
    public int damage = 1;
    public float startUpTime;
    public float recoveryTime;
    private Material mat;
    private Color oriColor;
    private AttackRange_Enemy attackRange_enemy;
    private Rigidbody rb;
    private Vector3 moveDirection;
    private bool isInRange = false;
    void Start()
    {
        attackRange_enemy = GetComponentInChildren<AttackRange_Enemy>();
        mat = GetComponentInChildren<Renderer>().material;
        rb = GetComponent<Rigidbody>();
        oriColor = mat.color;
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1) && curState == EnemyState.Idle)
        {
            StartCoroutine(Attack());

        }
        if (Input.GetKey(KeyCode.F2) && (curState == EnemyState.Idle || curState == EnemyState.Defending))
        {
            curState = EnemyState.Defending;
            mat.color = Color.blue;
        }
        if (Input.GetKeyUp(KeyCode.F2) && curState == EnemyState.Defending)
        {
            curState = EnemyState.Idle;
            mat.color = oriColor;
        }
        if (curState != EnemyState.Recovery && curState != EnemyState.Defending)
        {
            float horizontalInput = 0f;
            float verticalInput = 0f;

            if (Input.GetKey(KeyCode.UpArrow)) verticalInput += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) verticalInput -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) horizontalInput += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) horizontalInput -= 1f;

            moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;
            moveDirection.y = 0f;

            if (moveDirection != Vector3.zero)
            {
                enemyObj.forward = Vector3.Slerp(enemyObj.forward, moveDirection.normalized, Time.deltaTime * rotationSpeed);
            }
        }
    }
    void FixedUpdate()
    {
        if (moveDirection != Vector3.zero && curState != EnemyState.Recovery && curState != EnemyState.Defending)
        {
            Vector3 targetPos = rb.position + moveDirection.normalized * moveSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPos);
        }
    }
    IEnumerator Attack()
    {
        curState = EnemyState.StartUp;
        float time = 0f;
        Color targetColor = Color.red;
        // before attack
        while (time < startUpTime)
        {
            time += Time.deltaTime;
            float t = time / startUpTime;

            mat.color = Color.Lerp(oriColor, targetColor, t);
            yield return null;
        }
        curState = EnemyState.Attacking;
        time = 0f;
        mat.color = targetColor;
        //attacking
        isInRange = attackRange_enemy.IsInRange();
        if (isInRange)
        {
            HP player_HP = FindObjectOfType<HP>();
            player_HP.Hurt(damage);
            Debug.Log("Enemy attack success");
        }
        else
        {
            Debug.Log("Enemy attack fail");
        }
        // finish attack
        curState = EnemyState.Recovery;
        mat.color = Color.green;
        rb.velocity = Vector3.zero;
        while (time < recoveryTime)
        {
            time += Time.deltaTime;
            yield return null;
        }
        mat.color = oriColor;
        curState = EnemyState.Idle;
    }
}
