using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class SkinnedMeshColliderUpdater : MonoBehaviour
{
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private MeshCollider meshCollider;
    private Mesh bakedMesh;

    void Start()
    {
        skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
        bakedMesh = new Mesh();
    }

    void LateUpdate()
    {
        // 將當前動畫變形後的網格數據烘焙到 bakedMesh 中
        skinnedMeshRenderer.BakeMesh(bakedMesh);
        
        // 重新指派給 Mesh Collider 觸發物理更新
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = bakedMesh;
    }
}