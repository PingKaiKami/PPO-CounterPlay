using System;
using UnityEngine;

public class TrainingArea : MonoBehaviour
{
    public event Action OnEpisodeEnd;

    public void TriggerEpisodeEnd()
    {
        OnEpisodeEnd?.Invoke();
    }
    public Enemy_Agent enemyInArea;
    public Player_Behavior playerInArea;
}