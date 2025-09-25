using System; // 需要这个来使用 Action
using UnityEngine;

public static class GameEvents
{
    public static event Action OnEpisodeEnd;

    public static void TriggerEpisodeEnd()
    {
        OnEpisodeEnd?.Invoke();
    }
}