// DataRecorder.cs (简化版)

using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Transition 结构体保持不变
public struct Transition
{
    public List<float> state;
    public List<int> action; // 保持为 List<int>
    public float reward;
    public List<float> nextState;
    public bool done;
}

public class DataRecorder : MonoBehaviour
{
    [Tooltip("每个文件记录的最大回合数")]
    public int maxEpisodesPerFile = 50;
    
    [Tooltip("数据文件的保存路径 (相对于项目根目录)")]
    public string savePath = "OfflineData";

    private List<Transition> allTransitions = new List<Transition>();
    private int episodeCount = 0;
    
    // 公共接口，供 Agent 脚本调用
    public void Record(Transition transition)
    {
        allTransitions.Add(transition);
    }
    
    public void EndEpisode()
    {
        episodeCount++;
        if (episodeCount >= maxEpisodesPerFile)
        {
            Debug.Log($"已达到 {maxEpisodesPerFile} 回合，正在保存数据...");
            SaveDataToFile();
            allTransitions.Clear();
            episodeCount = 0;
            Debug.Log("数据已保存，开始新的记录周期。");
        }
    }

    private void Start()
    {
        // 确保目录存在
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", savePath));
    }

    private void OnApplicationQuit()
    {
        SaveDataToFile();
    }
    
    private void SaveDataToFile()
    {
        if (allTransitions.Count == 0) return;
        
        string filePath = Path.Combine(Application.dataPath, "..", savePath, $"unity_data_{System.DateTime.Now:yyyyMMdd_HHmmss}.json");
        
        // 使用 StringBuilder 高效构建 JSON
        StringBuilder sb = new StringBuilder();
        sb.Append("{\"data\":[");

        for (int i = 0; i < allTransitions.Count; i++)
        {
            var t = allTransitions[i];
            sb.Append("{");
            // 使用 string.Join 将 List<float> 和 List<int> 转换为逗号分隔的字符串
            sb.Append($"\"state\":[{string.Join(",", t.state)}],");
            sb.Append($"\"action\":[{string.Join(",", t.action)}],");
            sb.Append($"\"reward\":{t.reward.ToString(System.Globalization.CultureInfo.InvariantCulture)},"); // 保证小数点
            sb.Append($"\"next_state\":[{string.Join(",", t.nextState)}],");
            sb.Append($"\"done\":{t.done.ToString().ToLower()}");
            sb.Append("}");

            if (i < allTransitions.Count - 1)
            {
                sb.Append(",");
            }
        }
        sb.Append("]}");

        File.WriteAllText(filePath, sb.ToString());
        Debug.Log($"数据已成功保存到: {filePath}");
    }
}