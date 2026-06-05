using UnityEngine;

public class VR_EnemyWeapon : MonoBehaviour
{
    private Collider weaponCollider;
    private int damage = 25;
    private bool hasDealtDamage = false;

    void Awake()
    {
        foreach (Collider childCollider in GetComponentsInChildren<Collider>(true))
        {
            if (childCollider.CompareTag("Sword"))
            {
                weaponCollider = childCollider;
                break;
            }
        }
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
    }

    public void EnableWeaponHitbox()
    {
        hasDealtDamage = false;
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }
    }

    public void DisableWeaponHitbox()
    {
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasDealtDamage) return;

        if (other.CompareTag("Player"))
        {
            var playerHP = other.GetComponentInParent<VR_HP_Player_Demo>();
            if (playerHP != null)
            {
                hasDealtDamage = true;
                
                playerHP.HurtFromMelee(damage);
                
                Debug.Log($"<color=red>【敵方攻擊命中】</color> 玩家受到 {damage} 點傷害！");
            }
        }
        else if (other.CompareTag("Sword") || other.gameObject.layer == LayerMask.NameToLayer("Weapon"))
        {
            VR_EnemyDemo enemy = GetComponentInParent<VR_EnemyDemo>();
            if (enemy != null)
            {
                enemy.TriggerWeaponClashGuard();
            }
        }
    }
}