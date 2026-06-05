using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartGame : MonoBehaviour
{
    public Text label_text;
    public void OnClick()
    {
        DemoManager.Instance.ModelType = label_text.text;
        SceneManager.LoadScene("VRScene_Demo");
    }
}
