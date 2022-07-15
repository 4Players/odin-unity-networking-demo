using System.Collections;
using System.Collections.Generic;
using Odin.OdinNetworking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public TMP_InputField nameInputField;
    public Image hostImage;

    void Start()
    {
        nameInputField.onValueChanged.AddListener(OnNameChanged);
    }

    void OnNameChanged(string name)
    {
        OdinNetworkManager.Instance.LocalPlayer.Name = name;
    }

    // Update is called once per frame
    void Update()
    {
        if (OdinNetworkManager.Instance.IsHost())
        {
            hostImage.color = Color.green;
        }
        else
        {
            hostImage.color = Color.red;
        }
    }

    public void OnBodyColorChanged(int colorIndex)
    {
        var player = OdinNetworkManager.Instance.LocalPlayer.GetComponent<Player>();
        player.BodyColor = colorIndex;
    }
}
