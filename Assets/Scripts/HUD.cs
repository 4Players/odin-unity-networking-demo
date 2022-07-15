using System.Collections;
using System.Collections.Generic;
using Odin.OdinNetworking;
using Odin.OdinNetworking.Messages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public TMP_InputField nameInputField;
    public Image hostImage;
    public Button connectButton;
    public TMP_Dropdown bodyColor;

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

        connectButton.gameObject.SetActive(!OdinNetworkManager.Instance.IsConnected);
    }

    public void OnBodyColorChanged(int colorIndex)
    {
        if (OdinNetworkManager.Instance.IsConnected)
        {
            var player = OdinNetworkManager.Instance.LocalPlayer.GetComponent<Player>();
            player.BodyColor = colorIndex;    
        }
    }

    public void OnConnectPressed()
    {
        OdinUserDataUpdateMessage message = (OdinUserDataUpdateMessage)OdinNetworkManager.Instance.GetJoinMessage();
        message.SyncVars.Add(new OdinUserDataSyncVar("BodyColor", bodyColor.value));
        OdinNetworkManager.Instance.Connect(message);
    }
}
