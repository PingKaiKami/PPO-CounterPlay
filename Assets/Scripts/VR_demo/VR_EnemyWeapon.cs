using UnityEngine;

public class VR_EnemyWeapon : MonoBehaviour
{
    private Collider weaponCollider;
    private int currentDamage;
    private bool hasDealtDamage = false;

    void Awake()
    {
        foreach (Collider childCollider in GetComponentsInChildren<Collider>(true))
        {
            // 檢查哪一個子物件的 Tag 叫做 "Sword"
            if (childCollider.CompareTag("Sword"))
            {
                weaponCollider = childCollider;
                break; // 找到了就跳出迴圈
            }
        }
        // 遊戲一開始預設把武器 Collider 關閉，避免散步時誤傷玩家
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
    }

    /// <summary>
    /// 由 VR_EnemyDemo 在發動攻擊 (StartUp) 時呼叫，用來開啟碰撞與設定傷害
    /// </summary>
    public void EnableWeaponHitbox(int damage)
    {
        currentDamage = damage;
        hasDealtDamage = false; // 重設傷害標記，允許這一刀造成傷害
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }
    }

    /// <summary>
    /// 由 VR_EnemyDemo 在動畫結束時呼叫，關閉碰撞
    /// </summary>
    public void DisableWeaponHitbox()
    {
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. 如果這一刀已經砍過玩家了，就不重複計算傷害
        if (hasDealtDamage) return;

        // 2. 檢查撞到的是不是玩家 (請確保你的玩家物件或其 Hitbox 的 Tag 確實是 "Player")
        if (other.CompareTag("Player"))
        {
            // 嘗試抓取玩家的 HP 腳本
            var playerHP = other.GetComponentInParent<VR_HP_Player>();
            if (playerHP != null)
            {
                hasDealtDamage = true; // 標記為已造成傷害，防止一刀多段判定
                
                playerHP.HurtFromRanged(currentDamage); // 讓玩家扣血 (請根據你 VR_HP_Player 裡實際的扣血函式名稱修改，例如 TakeDamage 或 Hurt)
                
                Debug.Log($"<color=red>【敵方攻擊命中】</color> 玩家受到 {currentDamage} 點傷害！");
            }
        }
        else if (other.CompareTag("Sword") || other.gameObject.layer == LayerMask.NameToLayer("Weapon"))
        {
            // 找到大本體的 VR_EnemyDemo 腳本並通知牠被擋下了！
            VR_EnemyDemo enemy = GetComponentInParent<VR_EnemyDemo>();
            if (enemy != null)
            {
                enemy.TriggerWeaponClashGuard();
            }
        }
    }
}