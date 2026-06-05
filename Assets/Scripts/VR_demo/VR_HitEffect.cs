using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // 必須引入 URP 命名空間
using UnityEngine.SceneManagement;


public class VR_HitEffect : MonoBehaviour
{
    [Header("Volume Settings")]
    [SerializeField] private Volume globalVolume; // 拖入畫面的 Global Volume

    [Header("Hit Vignette Settings")]
    [SerializeField] private Color hitColor = Color.red;    // 受擊顏色（預設紅色）
    [SerializeField] private float maxIntensity = 1.0f;   // 受擊時的最大強度（建議 0.4 ~ 0.5，太高會擋住視線）
    [SerializeField] private float flashInDuration = 0.05f; // 紅光彈出的時間（越短越有衝擊感）
    [SerializeField] private float fadeOutDuration = 0.5f;  // 紅光淡出的時間

    [Header("Death Settings")]
    [SerializeField] private GameObject deathCanvas; // 拖入寫有 You Are Dead 的 World Space Canvas
    [SerializeField] private string menuSceneName = "Menu_Demo";

    private Vignette _vignette;
    private Coroutine _hitFlashCoroutine;

    void Awake()
    {
        // 檢查並獲取 Volume 中的 Vignette 設定
        if (globalVolume != null && globalVolume.profile.TryGet(out _vignette))
        {
            // 初始化：確保遊戲開始時特效是關閉的
            _vignette.active = true;
            _vignette.color.Override(hitColor);
            _vignette.intensity.Override(0f);
        }
        else
        {
            Debug.LogError("找不到 Global Volume 或 Profile 中沒有加入 Vignette 覆蓋！");
        }
    }

    // 外部呼叫的介面：當玩家受擊時，呼叫此 Function
    public void PlayHitEffect()
    {
        if (_vignette == null) return;

        // 如果上一次的受擊動畫還在跑，先強制停止，重新計算（應對連續受擊）
        if (_hitFlashCoroutine != null)
        {
            StopCoroutine(_hitFlashCoroutine);
        }

        _hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        float elapsedTime = 0f;

        // 1. 快速彈出紅光 (Flash In)
        while (elapsedTime < flashInDuration)
        {
            elapsedTime += Time.deltaTime;
            float lerpProgress = elapsedTime / flashInDuration;
            
            // 漸變強度到最大值
            _vignette.intensity.Override(Mathf.Lerp(0f, maxIntensity, lerpProgress));
            yield return null;
        }

        // 確保精準達到最大強度
        _vignette.intensity.Override(maxIntensity);

        // 2. 緩慢淡出紅光 (Fade Out)
        elapsedTime = 0f;
        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float lerpProgress = elapsedTime / fadeOutDuration;

            // 從最大強度漸變回 0
            _vignette.intensity.Override(Mathf.Lerp(maxIntensity, 0f, lerpProgress));
            yield return null;
        }

        // 完全關閉強度
        _vignette.intensity.Override(0f);
        _hitFlashCoroutine = null;
    }

    public void PlayDeathEffect()
    {
        if (_vignette == null) return;

        // 停止任何正在播放的受擊特效
        if (_hitFlashCoroutine != null)
        {
            StopCoroutine(_hitFlashCoroutine);
        }

        // 啟動死亡協程
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        float elapsedTime = 0f;
        // 讓死亡畫面彈出的速度跟受擊一樣快，或是稍微慢一點點展現窒息感
        float deathFlashDuration = 0.2f; 

        // 1. 畫面極速變全紅（將 Vignette 強度直接拉到最大 1.0）
        while (elapsedTime < deathFlashDuration)
        {
            elapsedTime += Time.deltaTime;
            float lerpProgress = elapsedTime / deathFlashDuration;
            
            // 死亡時強度拉到 1f，這會讓玩家眼睛四周到中心完全被紅色/黑色吞噬
            _vignette.intensity.Override(Mathf.Lerp(0f, 1f, lerpProgress));
            yield return null;
        }
        _vignette.intensity.Override(1f);

        // 2. 顯示 "You Are Dead" 字樣
        if (deathCanvas != null)
        {
            deathCanvas.SetActive(true);
            
            // 💡 VR 貼心細節：強制讓死亡字樣生成在玩家頭盔的正前方，防止玩家歪頭沒看到
            if (Camera.main != null)
            {
                deathCanvas.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
                deathCanvas.transform.rotation = Quaternion.LookRotation(deathCanvas.transform.position - Camera.main.transform.position);
            }
        }

        // 3. 畫面全紅並顯示字樣，靜止等待 1 秒鐘
        yield return new WaitForSeconds(3.0f);

        // 4. 回到主選單場景
        SceneManager.LoadScene(menuSceneName);
    }
}