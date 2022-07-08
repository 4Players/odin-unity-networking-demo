using System;
using System.Collections;
using System.Collections.Generic;
using OdinNetworking;
using UnityEngine;

public class Sphere : OdinNetworkedObject
{
    [OdinSyncVar] public int Color = 0;
    public List<Material> materials = new List<Material>();

    private void Start()
    {
        Invoke(nameof(ChangeColor), 5);
    }

    private void ChangeColor()
    {
        Color = 1;
    }

    private void Update()
    {
        if (Color > 0 && Color < materials.Count)
        {
            GetComponent<MeshRenderer>().sharedMaterial = materials[Color];
        }
    }
}
