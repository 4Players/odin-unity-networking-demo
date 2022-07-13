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

    protected void ReadSyncVars(List<OdinUserDataSyncVar> syncVars)
    {
        foreach (var syncVar in syncVars)
        {
            if (!_syncVars.ContainsKey(syncVar.Name))
            {
                Debug.LogError($"Sync variable {syncVar.Name} not available in object {gameObject.name}");
            }
            else
            {
                OdinSyncVarInfo syncInfo = _syncVars[syncVar.Name];
                if (syncInfo != null)
                {
                    var currentValue = syncInfo.FieldInfo.GetValue(this);
                    if (!currentValue.Equals(syncVar.Value))
                    {
                        OnSyncVarChanged(syncInfo, currentValue, syncVar.Value);
                    }
                }
            }
        }
    }
    
    private void OnSyncVarChanged(OdinSyncVarInfo syncInfo, object oldValue, object newValue)
    {
        syncInfo.FieldInfo.SetValue(this, newValue);

        if (!string.IsNullOrEmpty(syncInfo.OdinSyncVar.hook))
        {
            var hookMethod = this.GetType().GetMethod(syncInfo.OdinSyncVar.hook);
            if (hookMethod != null)
            {
                hookMethod.Invoke(this, new[]{oldValue, newValue});
            }
        }
    }
    
    public List<OdinUserDataSyncVar> CompileSyncVars()
    {
        var syncVars = new List<OdinUserDataSyncVar>();
        
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
                
                if (!currentValue.Equals(syncInfo.LastValue))
                {
                    OnSyncVarChanged(syncInfo, syncInfo.LastValue, currentValue);
                }
                
                syncInfo.LastValue = currentValue;
                numberOfDirtySyncVars++;
            }
        }
        
        foreach (var key in dirtySyncVars.Keys)
        {
            var syncVar = new OdinUserDataSyncVar(key, dirtySyncVars[key]);
            syncVars.Add(syncVar);
        }

        return syncVars;
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
