using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Odin.OdinNetworking;
using OdinNetworking;
using UnityEngine;

public class OdinNetworkItem : MonoBehaviour
{
    protected class OdinSyncVarInfo
    {
        public FieldInfo FieldInfo;
        public OdinSyncVar OdinSyncVar;
        public object LastValue;
            
        public void OnSerialize(OdinNetworkWriter writer, object instance)
        {
            object currentValue = FieldInfo.GetValue(instance); 
        }
    }
        
    protected Dictionary<string, OdinSyncVarInfo> _syncVars = new Dictionary<string, OdinSyncVarInfo>();

    public virtual void OnAwakeClient()
    {
        // Get Attributes
        foreach (var field in GetType().GetFields())
        {
            OdinSyncVar syncVar = (OdinSyncVar)Attribute.GetCustomAttribute(field, typeof(OdinSyncVar));
            if (syncVar != null)
            {
                Debug.Log($"Found sync var: {field.Name} with hook: {syncVar.hook}");

                _syncVars[field.Name] = new OdinSyncVarInfo { FieldInfo = field, OdinSyncVar = syncVar, LastValue = field.GetValue(this) };
            }
        }        
    }

    public virtual void OnStartClient()
    {

    }

    protected void ReadSyncVars(OdinNetworkReader reader)
    {
        var numberOfSyncVars = reader.ReadByte();
        if (numberOfSyncVars > 0)
        {
            for (byte i = 0; i < numberOfSyncVars; i++)
            {
                var syncVarName = reader.ReadString();
                var currentValue = reader.ReadObject();

                if (!_syncVars.ContainsKey(syncVarName))
                {
                    Debug.Log("SYNC VAR NOT AVAILABLE " + syncVarName);
                }
                OdinSyncVarInfo syncInfo = _syncVars[syncVarName];
                if (syncInfo != null)
                {
                    syncInfo.FieldInfo.SetValue(this, currentValue);    
                }
                else
                {
                    Debug.LogError($"Could not find Syncvar with name {syncVarName}");
                }                    
            }
        }
    }

    protected void WriteSyncVars(OdinNetworkWriter writer)
    {
        byte numberOfDirtySyncVars = 0;
        Dictionary<string, object> dirtySyncVars = new Dictionary<string, object>();
        foreach (var key in _syncVars.Keys)
        {
            OdinSyncVarInfo syncInfo = _syncVars[key];
            object currentValue = syncInfo.FieldInfo.GetValue(this);
            //if (!currentValue.Equals(syncInfo.LastValue))
            {
                //Debug.Log($"Value for SyncVar {key} changed. Old value: {syncInfo.LastValue}, new Value: {currentValue}");
                        
                dirtySyncVars[syncInfo.FieldInfo.Name] = currentValue;
                syncInfo.LastValue = currentValue;
                numberOfDirtySyncVars++;
            }
        }

        writer.Write(numberOfDirtySyncVars);
        foreach (var key in dirtySyncVars.Keys)
        {
            writer.Write(key);
            writer.Write(dirtySyncVars[key]);
        }
    }
    
    public virtual void OnStartLocalClient()
    {
    }

    public virtual void OnStopClient()
    {
            
    }

    public virtual void OnStopLocalClient()
    {
            
    }
}
