using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class VR_Sword : MonoBehaviour
{
    public Transform swordTip;
    private bool isAttacking = false;
    public bool IsAttacking() => isAttacking;
    public void StartAttack()
    {
        isAttacking = true;
    }
    public void EndAttack()
    {
        isAttacking = false;
    }

}
