using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VR_SwordDemo : MonoBehaviour
{
    [Header("Track Points")]
    public Transform bladeBase; // 劍柄上端
    public Transform bladeTip;  // 劍尖
    public int subDivisions = 4; // 劍身要細分幾個偵測點

    [Header("Settings")]
    public VR_Player_BehaviorDemo player;
    public LayerMask enemyLayer;
    public float CurrentSpeed { get; private set; }
    public float minVelocity = 2.0f; // 改用「線速度 (m/s)」比角速度更符合直覺

    [Header("Knockback Settings")]
    [Tooltip("擊飛力道強度")]
    public float knockbackForce = 15f;
    [Tooltip("往上挑飛的力道（讓擊飛有拋物線感，看起來更帥）")]
    public float upwardModify = 2f;

    private Vector3[] lastPoints;
    private float lastDamageTime;
    private const float damageCooldown = 0.3f;

    void Start()
    {
        // 初始化紀錄點的位置
        lastPoints = GetBladePoints();
        if (player.IsTesting)
        {
            GetComponent<Collider>().enabled = false;
            XRGrabInteractable grab = GetComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;

        }
        else
        {
            GetComponent<Collider>().enabled = true;
            XRGrabInteractable grab = GetComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        }
    }

    void FixedUpdate()
    {
        Vector3[] currentPoints = GetBladePoints();
        
        // 計算這一幀劍尖的移動速度 (線速度)
        float speed = (currentPoints[currentPoints.Length - 1] - lastPoints[lastPoints.Length - 1]).magnitude / Time.fixedDeltaTime;

        CurrentSpeed = speed;

        if (Time.time > lastDamageTime + damageCooldown)
        {
            // 遍歷所有細分點，將「上一幀的位置」與「這一幀的位置」連線
            for (int i = 0; i < currentPoints.Length; i++)
            {
                RaycastHit hit;
                // 用 Linecast 檢查這一幀與上一幀之間，有沒有任何怪物穿過這條線
                if (Physics.Linecast(lastPoints[i], currentPoints[i], out hit, enemyLayer))
                {
                    if (speed >= minVelocity)
                    {
                        TriggerDamage(hit, speed);
                        break; // 砍中一次就跳出，避免多點重複判定
                    }
                    else
                    {
                        Debug.Log($"【力道不足】速度僅有: {speed:F2} m/s");
                    }
                }
            }
        }

        // 紀錄這一幀的位置給下一幀使用
        lastPoints = currentPoints;
    }

    // 動態計算劍身上的細分點位置
    Vector3[] GetBladePoints()
    {
        Vector3[] points = new Vector3[subDivisions];
        for (int i = 0; i < subDivisions; i++)
        {
            float t = (float)i / (subDivisions - 1);
            points[i] = Vector3.Lerp(bladeBase.position, bladeTip.position, t);
        }
        return points;
    }

    void TriggerDamage(RaycastHit hit, float speed)
    {
        lastDamageTime = Time.time;
        
        // 1. 取得怪物的 HP 腳本
        var enemyHP = hit.collider.GetComponentInParent<VR_HP_Enemy>();
        if (enemyHP != null)
        {
            enemyHP.HurtFromMelee(player.damage); 
        }

        // 2. 【核心邏輯】處理擊飛效果
        Rigidbody enemyRigidbody = hit.collider.GetComponentInParent<Rigidbody>();
        
        if (enemyRigidbody != null)
        {
            // 如果怪物有導航系統 (NavMeshAgent)，必須先關閉它，否則會飛不起來
            var agent = enemyRigidbody.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false; 
                // 備註：你需要在怪物的腳本裡寫一個定時器，例如 0.5 秒後把 agent.enabled 改回 true 讓牠重新站起來
            }

            // 計算擊飛方向：
            // 做法 A：沿著「劍刃揮舞的方向」擊飛（最真實）
            // 這裡我們用這一幀劍尖的位置減去上一幀，得到揮劍的向量，並轉為單位向量
            Vector3 slashDirection = (bladeTip.position - lastPoints[lastPoints.Length - 1]).normalized;

            // 稍微加一點往上的力（upwardModify），怪物才會往斜上方飛，產生漂亮的拋物線
            slashDirection.y += upwardModify;
            slashDirection = slashDirection.normalized;

            // 3. 施加力道！
            // ForceMode.Impulse 代表「瞬間衝擊力」，最適合用在爆炸、砍擊
            enemyRigidbody.AddForce(slashDirection * knockbackForce, ForceMode.Impulse);
            
            // 如果想增加打擊感，可以在砍中點生成火花特效
            // Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        }
    }
}