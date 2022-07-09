using System;
using System.Collections;
using System.Collections.Generic;
using OdinNetworking;
using UnityEngine;

public class Sphere : OdinNetworkedObject
{
    [OdinSyncVar(hook=nameof(ColorChanged))] public int Color = 0;
    public List<Material> materials = new List<Material>();

    private void Start()
    {
        Invoke(nameof(ChangeColor), 5);
    }

    private void ChangeColor()
    {
        Color = 1;
    }

    public void ColorChanged(int oldColor, int newColor)
    {
        Debug.Log($"COLOR CHANGED FROM {oldColor} TO {newColor}");
    }

    private void Update()
    {
        if (Color > 0 && Color < materials.Count)
        {
            GetComponent<MeshRenderer>().sharedMaterial = materials[Color];
        }
    }
}
