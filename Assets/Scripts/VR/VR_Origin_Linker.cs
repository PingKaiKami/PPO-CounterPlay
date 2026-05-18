using UnityEngine;

public class VROriginLinker : MonoBehaviour
{
    public Transform playerHead; // 指定 Player 底下的 Head
    public Transform playerBody; // 指定 Player 物件
    public Camera vrCamera;
    public VR_Player_Behavior player;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (playerHead == null || playerBody == null || vrCamera == null) return;

        // 1. 位置同步 (保持不變)
        Vector3 cameraOffset = vrCamera.transform.position - transform.position;
        transform.position = playerHead.position - cameraOffset;

        Vector3 camForward = vrCamera.transform.forward;
        
        // 2. 旋轉同步 (讓身體跟隨相機視線)
        // 關鍵：抹除 Y 軸，防止角色因為玩家抬頭/低頭而傾斜
        camForward.y = 0; 

        if(player.curMode == VR_Player_Behavior.TrainingMode.Manual)
        {
            if (camForward.sqrMagnitude > 0.01f)
            {
                playerBody.rotation = Quaternion.LookRotation(camForward.normalized);
            }
        }
        else
        {
            transform.rotation = playerBody.rotation;
        }
    }
}