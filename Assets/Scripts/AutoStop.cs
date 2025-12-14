using UnityEngine;
// 1. 將引用 UnityEditor 包起來
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class AutoStopPlay : MonoBehaviour
{
    [Tooltip("自動停止的時間（秒）")]
    public float stopAfterSeconds = 600f; // 預設10分鐘

    // 2. 將變數與邏輯也包起來，避免打包時出現 "變數未被使用" 的警告或錯誤
#if UNITY_EDITOR
    private float startTime;
    private bool isTracking = false;

    void OnEnable()
    {
        // 確保是在播放模式下才執行
        if (EditorApplication.isPlaying)
        {
            startTime = Time.realtimeSinceStartup;
            isTracking = true;
            EditorApplication.update += CheckTime;
            Debug.Log($"[AutoStopPlay] 已啟動，{stopAfterSeconds} 秒後將自動停止。");
        }
    }

    void OnDisable()
    {
        EditorApplication.update -= CheckTime;
        isTracking = false;
    }

    private void CheckTime()
    {
        if (!EditorApplication.isPlaying || !isTracking)
            return;

        if (Time.realtimeSinceStartup - startTime >= stopAfterSeconds)
        {
            Debug.Log("[AutoStopPlay] 已達指定時間，自動停止。");
            EditorApplication.isPlaying = false;
        }
    }
#endif
}