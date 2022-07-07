using System.Collections;
using System.Collections.Generic;
using Odin.OdinNetworking;
using TMPro;
using UnityEngine;

public class HUD : MonoBehaviour
{
    public TMP_InputField nameInputField;

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
        
    }
}
