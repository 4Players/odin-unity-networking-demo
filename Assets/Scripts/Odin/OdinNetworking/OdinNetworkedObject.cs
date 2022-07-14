using System;
using System.Collections;
using System.Collections.Generic;
using ElRaccoone.Tweens;
using Odin.OdinNetworking;
using OdinNative.Odin.Peer;
using Unity.Collections;
using UnityEngine;

public class OdinNetworkedObject : OdinNetworkItem
{
    [HideInInspector]
    public OdinNetworkItem Owner;
    [HideInInspector]
    public byte ObjectId;
    [HideInInspector]
    public byte PrefabId;

    [HideInInspector] 
    public bool IsUpdated = false;
    
    [Tooltip("This defines the lifetime of this object in seconds. If zero, it needs to be destroyed manually.")]
    public float LifeTime = 0;

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (LifeTime > 0)
        {
            StartCoroutine(SelfDestroy(LifeTime));
        }
    }
    
    public IEnumerator SelfDestroy(float f)
    {
        yield return new WaitForSeconds(f);
        Owner.DestroyNetworkedObject(this);
    }

    public static (byte, byte) DeserializeHeader(OdinNetworkReader reader)
    {
        var objectId = reader.ReadByte();
        var prefabId = reader.ReadByte();
        return (objectId, prefabId);
    }

    public void OnUpdatedFromNetwork(OdinUserDataManagedObject managedObject, bool tween = true)
    {
        if (tween)
        {
            this.TweenLocalPosition(managedObject.Transform.Position, Owner.SendInterval);
            this.TweenLocalRotation(managedObject.Transform.Rotation.eulerAngles, Owner.SendInterval);
            this.TweenLocalScale(managedObject.Transform.Scale, Owner.SendInterval);    
        }
        else
        {
            transform.localPosition = managedObject.Transform.Position;
            transform.localRotation = managedObject.Transform.Rotation;
            transform.localScale = managedObject.Transform.Scale;
        }
        
        ReadSyncVars(managedObject.SyncVars);
    }
}
