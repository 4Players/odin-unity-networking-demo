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
    public OdinNetworkIdentity Owner;
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

    public void SerializeHeader(OdinNetworkWriter writer)
    {
        writer.Write(ObjectId);
        writer.Write(PrefabId);
    }

    public void SerializeBody(OdinNetworkWriter writer)
    {
        writer.Write(gameObject.transform);
        WriteSyncVars(writer);
    }

    public static (byte, byte) DeserializeHeader(OdinNetworkReader reader)
    {
        var objectId = reader.ReadByte();
        var prefabId = reader.ReadByte();
        return (objectId, prefabId);
    }

    public void UpdateFromReader(OdinNetworkReader reader, bool tween = true)
    {
        var (localPosition, localRotation, localScale) = reader.ReadTransform();
        if (tween)
        {
            this.TweenLocalPosition(localPosition, Owner.SendInterval);
            this.TweenLocalRotation(localRotation.eulerAngles, Owner.SendInterval);
            this.TweenLocalScale(localScale, Owner.SendInterval);    
        }
        else
        {
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
        }
        
        ReadSyncVars(reader);
    }
}
