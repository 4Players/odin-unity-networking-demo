using System;
using System.Collections.Generic;
using System.Reflection;
using Odin.OdinNetworking.Messages;
using UnityEngine;

namespace Odin.OdinNetworking
{
    /// <summary>
    /// This is the base class of all networked objects in OdinNetworking. It provides basic settings like the
    /// send interval (how many updates of data per second) and stores lists of managed objects and sync vars
    /// that are also synced over the network.
    /// </summary>
    public class OdinNetworkItem : MonoBehaviour
    {
        /// <summary>
        /// Stores sync vars and the last value of this object. 
        /// </summary>
        public class OdinSyncVarInfo
        {
            public FieldInfo FieldInfo;
            public OdinSyncVar OdinSyncVar;
            public object LastValue;
            
            public void OnSerialize(OdinNetworkWriter writer, object instance)
            {
                object currentValue = FieldInfo.GetValue(instance); 
            }
        }
    
        /// <summary>
        /// The interval in seconds how often this objects sends updates of its own state (and its managed objects)
        /// over the network. To save bandwidth this value should be as high as possible. A good value is 0.1 which
        /// means, that every 1/10th of a second and update is sent, i.e. 10 times as second.
        /// </summary>
        [Tooltip("The number of seconds until the next update is sent")]
        public float SendInterval = 0.1f;
    
        /// <summary>
        /// A list of managed objects. Managed objects are <see cref="Odin.OdinNetworking.OdinNetworkedObject"/> that
        /// are owned by a <see cref="Odin.OdinNetworking.OdinNetworkItem"/> and have a position and sync vars that is
        /// updated by the owner.
        /// </summary>
        public List<OdinNetworkedObject> ManagedObjects { get; } = new List<OdinNetworkedObject>();
        
        /// <summary>
        /// A counter that is incremented for each managed object and gives every managed object a unique id.
        /// </summary>
        protected byte _objectId = 0;
        
        /// <summary>
        /// A dictionary of sync vars. Sync vars are member properties with an <see cref="Odin.OdinNetworking.OdinSyncVar"/>
        /// attribute. These properties are synced over the network. Whenever the local player changes one of it's sync vars
        /// the updated value is sent over the network and updated on all clients.
        /// </summary>
        protected Dictionary<string, OdinSyncVarInfo> _syncVars = new Dictionary<string, OdinSyncVarInfo>();
    
        /// <summary>
        /// The last message sent over the network. Used to compare the current message to prevent sending updates
        /// without any changes to it.
        /// </summary>
        protected OdinNetworkWriter _lastUserData = null;
        
        /// <summary>
        /// The time when the last message has been sent. Used in combination with SendInterval to figure out when the
        /// next update should be sent.
        /// </summary>
        protected float _lastSent;

        /// <summary>
        /// A helper function to set the isKinetic flag on all rigid bodies in the hierarchy of this object
        /// </summary>
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
        
        /// <summary>
        /// Returns if this object is the local player (true) or a client (false).
        /// </summary>
        /// <remarks>All players in the scene are OdinNetworkItem objects, but the local player is just controlling one of them.
        /// Use this flag to figure out if this object is the local player or a remote client. For example it's important to
        /// change the position of the player only on input if its the local player.</remarks>
        public bool IsLocalPlayer => OdinNetworkManager.Instance.LocalPlayer == this;
        
        /// <summary>
        /// Every world has exactly one host that is responsible for handling the worlds state. The host is determined by an algorithm
        /// and can change everytime. It's more a virtual host, i.e. one player in the same room is given a host flag, and if that player
        /// disconnects, or even another player connects the host flag might be set on another player.
        /// <remarks>Every object in the world must have exactly one identity that has ownership and handles its state. Otherwise
        /// it would be a big mess if everyone could change all objects at the same time in the network. Objects spawned by the
        /// players into the world have a clear authority as the spawner has ownership. But objects that are part of the world
        /// and have been there from the very beginning don't have ownership. Therefore, the host flag exists. The host gets ownership
        /// for these world objects and updates the state regularly as part of its own update cycle. If the host disconnects
        /// another player becomes host and is responsible for updating the world. Other clients can change the world by sending
        /// command messages that are processed by the current host.
        /// Think of a bulb that can be switched off in the scene by multiple switches. If every player could change the state
        /// of the bulb it would be very hard to make sure that every player sees the same representation of the world. Therefore
        /// only the host manages the bulbs on/off state while the other players send commands to request a change.</remarks>
        /// </summary>
        public bool IsHost => OdinNetworkManager.Instance.LocalPlayer.Peer.Id == OdinNetworkManager.Instance.GetHost().Id;

        /// <summary>
        /// This function is called by the <see cref="Odin.OdinNetworking.OdinNetworkManager"/> after this item has been spawned
        /// or activated in the world. It sets up the sync vars, i.e. a list of properties that should be synced over the network. 
        /// </summary>
        /// <remarks>Please call the base class implementation if you override this function, otherwise sync vars will not work!</remarks>
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

        /// <summary>
        /// Called before the regular Unity Start function for every client connected to the room. 
        /// </summary>
        /// <remarks>The default implementation does nothing.</remarks>
        public virtual void OnStartClient()
        {

        }

        /// <summary>
        /// Reads a list sync vars that were part of an update message and updates the local values by calling
        /// the <see cref="Odin.OdinNetworking.OdinNetworkItem.OnSyncVarChanged"/> function.
        /// </summary>
        /// <param name="syncVars">A list of sync var values with their previous and current value.</param>
        public void ReadSyncVars(List<OdinUserDataSyncVar> syncVars)
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
    
        /// <summary>
        /// Sets the current value and calls the hook function if available (i.e. OnSomethingChanged).
        /// </summary>
        /// <param name="syncInfo">The sync var structure that has been changed</param>
        /// <param name="oldValue">The old value of the sync var before the update</param>
        /// <param name="newValue">The new value that has been received from the server</param>
        protected virtual void OnSyncVarChanged(OdinSyncVarInfo syncInfo, object oldValue, object newValue)
        {
            syncInfo.FieldInfo.SetValue(this, newValue);

            if (!string.IsNullOrEmpty(syncInfo.OdinSyncVar.hook))
            {
                var hookMethod = this.GetType().GetMethod(syncInfo.OdinSyncVar.hook, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (hookMethod != null)
                {
                    hookMethod.Invoke(this, new[]{oldValue, newValue});
                }
            }
        }
        
        public virtual List<OdinSyncVarInfo> GetDirtySyncVars()
        {
            List<OdinSyncVarInfo> dirtySyncVars = new List<OdinSyncVarInfo>();
            foreach (var key in _syncVars.Keys)
            {
                OdinSyncVarInfo syncInfo = _syncVars[key];
                object currentValue = syncInfo.FieldInfo.GetValue(this);
                
                if (!currentValue.Equals(syncInfo.LastValue))
                {
                    if (!string.IsNullOrEmpty(syncInfo.OdinSyncVar.hook))
                    {
                        var hookMethod = this.GetType().GetMethod(syncInfo.OdinSyncVar.hook, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (hookMethod != null)
                        {
                            hookMethod.Invoke(this, new[]{syncInfo.LastValue, currentValue});
                        }
                    }
                    
                    //Debug.Log($"Value for SyncVar {key} changed. Old value: {syncInfo.LastValue}, new Value: {currentValue}");
                    dirtySyncVars.Add(syncInfo);
                    syncInfo.LastValue = currentValue;
                }
            }

            return dirtySyncVars;
        }
    
        /// <summary>
        /// Compiles the state of all sync vars of this object into a list of message components that are sent as part
        /// of an update message over the network.
        /// </summary>
        /// <returns></returns>
        public List<OdinUserDataSyncVar> CompileSyncVars()
        {
            var syncVars = new List<OdinUserDataSyncVar>();
        
            byte numberOfDirtySyncVars = 0;
            Dictionary<string, object> dirtySyncVars = new Dictionary<string, object>();
            foreach (var key in _syncVars.Keys)
            {
                OdinSyncVarInfo syncInfo = _syncVars[key];
                object currentValue = syncInfo.FieldInfo.GetValue(this);
                
                // We always send all sync vars as they are part of a persistent data structure that represents the whole
                // player state and not just the changes.
                
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

        public void SetSyncVarValue(string name, object value)
        {
            OdinSyncVarInfo syncInfo = _syncVars[name];
            if (syncInfo != null)
            {
                object oldValue = syncInfo.FieldInfo.GetValue(this);
                
                syncInfo.FieldInfo.SetValue(this, value);
                syncInfo.LastValue = value;
                
                if (!string.IsNullOrEmpty(syncInfo.OdinSyncVar.hook))
                {
                    var hookMethod = this.GetType().GetMethod(syncInfo.OdinSyncVar.hook, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (hookMethod != null)
                    {
                        hookMethod.Invoke(this, new[]{oldValue, value});
                    }
                }
            }
        }
    
        /// <summary>
        /// Callback function that is only called on the local player. Use it to attach the camera to the player or
        /// activate additional scripts that only the local player should have (like handling input).
        /// </summary>
        /// <remarks>The default implementation does nothing.</remarks>
        public virtual void OnStartLocalClient()
        {
        }

        /// <summary>
        /// Called once a remote client has been disconnected from the server (i.e. before it's destroyed). Use that
        /// function to update a table with the currently connected peers for example.
        /// </summary>
        /// <remarks>The default implementation does nothing.</remarks>
        public virtual void OnStopClient()
        {
            
        }

        /// <summary>
        /// Called only on the local player before getting disconnected from the server. Use it to show a connect button
        /// or to bring the player back to a main menu.
        /// </summary>
        /// <remarks>The default implementation does nothing.</remarks>
        public virtual void OnStopLocalClient()
        {
            
        }

        /// <summary>
        /// Spawn a prefab in the world. This object will become the owner of the spawned object and will send updates
        /// of it's state as part of the update cycle.
        /// </summary>
        /// <remarks>The prefab must be added to the <see cref="Odin.OdinNetworking.OdinNetworkManager.spawnablePrefabs"/>
        /// list. As strings require a lot of bytes over the network, prefabs are added to a list and if you spawn a prefab
        /// only the index in the list is sent over the network. It also makes sure that only allowed prefabs get spawned.</remarks>
        /// <param name="prefab">The prefab that should be spawned over the network. Must be entered in the spawnablePrefabs
        /// list of the <see cref="Odin.OdinNetworking.OdinNetworkManager"/> instance.</param>
        /// <param name="position">The position where to spawn the object</param>
        /// <param name="rotation">The rotation of the spawned object in its initial state</param>
        public void SpawnManagedNetworkedObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            SpawnManagedNetworkedObject(prefab.name, position, rotation);
        }

        /// <summary>
        /// Add a networked object to the list of the managed objects. The owner will be set and it will be
        /// added to the ManagedObjects list.
        /// </summary>
        /// <param name="networkedObject">The object that this object should take control.</param>
        public void AddToManagedObjectsList(OdinNetworkedObject networkedObject)
        {
            if (networkedObject.Owner == null)
            {
                networkedObject.Owner = this;
            }
        
            ManagedObjects.Add(networkedObject);
            _objectId++;
        }

        /// <summary>
        /// Spawn a prefab in the world by its PrefabId. This object will become the owner of the spawned object and will send updates
        /// of it's state as part of the update cycle.
        /// </summary>
        /// <remarks>The prefab must be added to the <see cref="Odin.OdinNetworking.OdinNetworkManager.spawnablePrefabs"/>
        /// list. As strings require a lot of bytes over the network, prefabs are added to a list and if you spawn a prefab
        /// only the index in the list is sent over the network. It also makes sure that only allowed prefabs get spawned.</remarks>
        /// <param name="prefabId">The index of the prefab in the spawnablePrefabs 
        /// list of the <see cref="Odin.OdinNetworking.OdinNetworkManager"/> instance.</param>
        /// <param name="position">The position where to spawn the object</param>
        /// <param name="rotation">The rotation of the spawned object in its initial state</param>
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

        /// <summary>
        /// Spawn a prefab in the world by its name. This object will become the owner of the spawned object and will send updates
        /// of it's state as part of the update cycle.
        /// </summary>
        /// <remarks>The prefab must be added to the <see cref="Odin.OdinNetworking.OdinNetworkManager.spawnablePrefabs"/>
        /// list. As strings require a lot of bytes over the network, prefabs are added to a list and if you spawn a prefab
        /// only the index in the list is sent over the network. It also makes sure that only allowed prefabs get spawned.</remarks>
        /// <param name="prefabName">The name of the prefab. Must be in the spawnablePrefabs 
        /// list of the <see cref="Odin.OdinNetworking.OdinNetworkManager"/> instance.</param>
        /// <param name="position">The position where to spawn the object</param>
        /// <param name="rotation">The rotation of the spawned object in its initial state</param>
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

        /// <summary>
        /// Spawn an object into the world. This object will not be part of any update cycle and should have a Lifetime greater
        /// than 0 so it self destroys after some time. The system makes sure that the object is spawned an all clients but then
        /// those only live on all clients. Use these kind of objects for gimmicks like particle systems or decoration where its
        /// not important to the experience that every player sees exactly the same state of the object.
        /// </summary>
        /// <remarks>The prefab must be added to the <see cref="Odin.OdinNetworking.OdinNetworkManager.spawnablePrefabs"/>
        /// list. As strings require a lot of bytes over the network, prefabs are added to a list and if you spawn a prefab
        /// only the index in the list is sent over the network. It also makes sure that only allowed prefabs get spawned.</remarks>
        /// <param name="prefabName">The name of the prefab that should be spawned.</param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
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

            // TODO: These objects might not need an object id as they are never referred to in the network?
            _objectId++;
        }

        /// <summary>
        /// Destroy a managed networked object and make sure it's destroyed on all other clients, too. 
        /// </summary>
        /// <remarks>You can only destroy managed object that you have ownership. If you want to destroy another owners
        /// managed object then you need to send a custom message to him and then the owner can decide if the object
        /// should be destroyed or not.</remarks>
        /// <param name="networkedObject">The networked object that should be destroyed</param>
        public void DestroyManagedNetworkedObject(OdinNetworkedObject networkedObject)
        {
            if (networkedObject.Owner != this)
            {
                Debug.LogWarning($"Could not destroy networked object as I am not the owner of it");
                return;
            }

            ManagedObjects.Remove(networkedObject);
            DestroyImmediate(networkedObject.gameObject);
        }
    
        /// <summary>
        /// Destroy all managed object that have not been updated in the last update cycle. As the update cycle contains all currently
        /// active objects this means, that these objects have been removed in the meantime and should be removed here too.
        /// </summary>
        protected void DestroyDeprecatedSpawnedObjects()
        {
            foreach (var spawnedObject in ManagedObjects.ToArray())
            {
                if (spawnedObject.IsUpdated == false)
                {
                    DestroyManagedNetworkedObject(spawnedObject);
                }
            }
        }

        /// <summary>
        /// Set the IsUpdated flag on all managed objects to prepare the update cycle.
        /// </summary>
        protected void PreparedSpawnedObjectsForUpdate()
        {
            foreach (var spawnedObject in ManagedObjects)
            {
                spawnedObject.IsUpdated = false;
            }
        }
    }
}
