using System;
using System.Collections;
using System.Collections.Generic;
using Odin.OdinNetworking;
using OdinNetworking;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class DemoWorld : OdinWorld
{
    [OdinSyncVar(hook = nameof(OnLightEnabled))]
    public bool lightEnabled = true;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnLightEnabled(bool oldValue, bool newValue)
    {
        var light = FindObjectOfType<Light>();
        light.intensity = lightEnabled ? 1.0f : 0.2f;
    }
}
