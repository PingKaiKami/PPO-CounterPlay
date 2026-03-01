using System;
using UnityEngine;

public class VR_TrainingArea : MonoBehaviour
{
    public event Action OnEpisodeEnd;

    public void TriggerEpisodeEnd()
    {
        OnEpisodeEnd?.Invoke();
    }
    public VR_Enemy enemyInArea;
    public VR_Player playerInArea;
}
