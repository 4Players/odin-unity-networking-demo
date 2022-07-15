using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Odin.OdinNetworking;
using Odin.OdinNetworking.Messages;
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
    
    [Tooltip("The number of seconds until the next update is sent")]
    public float SendInterval = 0.1f;
    
    public List<OdinNetworkedObject> ManagedObjects { get; } = new List<OdinNetworkedObject>();
    protected byte _objectId = 0;
        
    protected Dictionary<string, OdinSyncVarInfo> _syncVars = new Dictionary<string, OdinSyncVarInfo>();
    
    protected OdinNetworkWriter _lastUserData = null;
    protected OdinNetworkWriter _lastNetworkedObjectUpdate = null;
    protected float _lastSent;

    public bool IsKinetic
    {
        set
        {
            // Set objects to be kinetic per default
            foreach (var rb in this.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = value;
            }    
        }
    }

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
    
    public bool IsLocalPlayer()
    {
        return OdinNetworkManager.Instance.LocalPlayer == this;
    }
    
    public bool IsHost()
    {
        return OdinNetworkManager.Instance.LocalPlayer == OdinNetworkManager.Instance.Host;
    }
    
    public void SpawnManagedNetworkedObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        SpawnManagedNetworkedObject(prefab.name, position, rotation);
    }

    public void AddToManagedObjectsList(OdinNetworkedObject networkedObject)
    {
        if (networkedObject.Owner == null)
        {
            networkedObject.Owner = this;
        }
        
        ManagedObjects.Add(networkedObject);
        _objectId++;
    }

    public void SpawnManagedNetworkedObject(byte prefabId, Vector3 position, Quaternion rotation)
    {
        var networkedObject = OdinNetworkManager.Instance.SpawnPrefab(this, prefabId, _objectId, position, rotation);
        if (networkedObject == null)
        {
            Debug.LogWarning($"Could not spawn prefab with id {prefabId}");
            return;
        }

        AddToManagedObjectsList(networkedObject);
    }

    public void SpawnManagedNetworkedObject(string prefabName, Vector3 position, Quaternion rotation)
    {
        var networkedObject = OdinNetworkManager.Instance.SpawnPrefab(this, prefabName, _objectId, position, rotation);
        if (networkedObject == null)
        {
            Debug.LogWarning($"Could not spawn prefab {prefabName}");
            return;
        }

        AddToManagedObjectsList(networkedObject);
    }

    public void SpawnNetworkedObject(string prefabName, Vector3 position, Quaternion rotation)
    {
        var networkedObject = OdinNetworkManager.Instance.SpawnPrefab(this, prefabName, _objectId, position, rotation);
        if (networkedObject == null)
        {
            Debug.LogWarning($"Could not spawn prefab {prefabName}");
            return;
        }

        OdinSpawnPrefabMessage message =
            new OdinSpawnPrefabMessage(networkedObject.PrefabId, _objectId, position, rotation);
        OdinNetworkManager.Instance.SendMessage(message, false);

        _objectId++;
    }


    public void DestroyNetworkedObject(OdinNetworkedObject networkedObject)
    {
        if (networkedObject.Owner != this)
        {
            Debug.LogWarning($"Could not destroy networked object as I am not the owner of it");
            return;
        }

        ManagedObjects.Remove(networkedObject);
        DestroyImmediate(networkedObject.gameObject);
    }
    
    protected void DestroyDeprecatedSpawnedObjects()
    {
        foreach (var spawnedObject in ManagedObjects.ToArray())
        {
            if (spawnedObject.IsUpdated == false)
            {
                DestroyNetworkedObject(spawnedObject);
            }
        }
    }

    protected void PreparedSpawnedObjectsForUpdate()
    {
        foreach (var spawnedObject in ManagedObjects)
        {
            spawnedObject.IsUpdated = false;
        }
    }
}
