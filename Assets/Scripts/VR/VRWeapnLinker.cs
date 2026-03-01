using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VRWeaponLinker : MonoBehaviour
{
    public Transform Hand; // Player 底下的 Sword_Pivot
    public Transform swordPivot; // 移到根目錄後的 Sword 物件

    [Header("Settings")]
    public bool smoothFollow = false;
    public float followSpeed = 20f;

    void FixedUpdate()
    {
        if (Hand == null || swordPivot == null) return;
        
        if (!smoothFollow)
        {
            // 瞬間同步 (最穩定，不會有延遲感)
            swordPivot.position = Hand.position;
            swordPivot.rotation = Hand.rotation;
        }
        else
        {
            // 平滑同步 (看起來比較有重量感)
            swordPivot.position = Vector3.Lerp(swordPivot.position, Hand.position, Time.deltaTime * followSpeed);
            swordPivot.rotation = Quaternion.Slerp(swordPivot.rotation, Hand.rotation, Time.deltaTime * followSpeed);
        }
    }
}
