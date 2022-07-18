using Odin.OdinNetworking.Messages;
using UnityEngine;

namespace Odin.OdinNetworking
{
    public class OdinWorld : OdinNetworkItem
    {
        public static OdinWorld Instance { get; private set; }
        
        private OdinNetworkIdentity _owner;

        public void SetOwner(OdinNetworkIdentity owner)
        {
            _owner = owner;
            foreach (var networkedObject in ManagedObjects)
            {
                networkedObject.IsKinetic = !owner.IsHost();                
            }
        }

        public OdinNetworkedObject GetNetworkObject(byte objectId)
        {
            foreach (var networkedObject in ManagedObjects)
            {
                if (networkedObject.ObjectId == objectId)
                {
                    return networkedObject;
                }
            }

            return null;
        }

        public OdinNetworkedObject GetNetworkObject(GameObject go)
        {
            foreach (var networkedObject in ManagedObjects)
            {
                if (networkedObject.gameObject == go)
                {
                    return networkedObject;
                }
            }

            return null;
        }
        
        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }
            
            foreach (var networkedObject in FindObjectsOfType<OdinNetworkedObject>())
            {
                if (networkedObject.Owner == null)
                {
                    AddToManagedObjectsList(networkedObject);
                }
            }
            
            OnAwakeClient();
        }
        
        private void FixedUpdate()
        {
            return;
            
            if (!IsHost())
            {
                return;
            }
            
            // Wait for the next slot for sending data
            if (Time.time - _lastSent > SendInterval)
            {
                UpdateUserData();
            }
        }

        public OdinNetworkWriter CompileUserData()
        {
            OdinWorldUpdateMessage message = new OdinWorldUpdateMessage();
                
            message.SyncVars = CompileSyncVars();
            
            foreach (var networkedObject in ManagedObjects)
            {
                var transform = new OdinUserDataTransform(networkedObject.transform.localPosition, networkedObject.transform.localRotation, networkedObject.transform.localScale);
                var managedObject =
                    new OdinUserDataManagedObject(networkedObject.ObjectId, networkedObject.PrefabId, transform);
                managedObject.SyncVars = networkedObject.CompileSyncVars();
                message.ManagedObjects.Add(managedObject);
            }

            return message.GetWriter();
        }
        
        public virtual void OnUpdatedFromNetwork(OdinWorldUpdateMessage message)
        {
            ReadSyncVars(message.SyncVars);
            
            // Networked Objects
            if (message.ManagedObjects.Count > 0)
            {
                PreparedSpawnedObjectsForUpdate();
            
                foreach (var managedObject in message.ManagedObjects)
                {
                    var networkedObject = ManagedObjects[managedObject.ObjectId];
                    if (networkedObject)
                    {
                        networkedObject.OnUpdatedFromNetwork(managedObject, true);
                        networkedObject.IsUpdated = true;
                    }
                }
            
                // Walk through all spawned objects and remove those that were not in the update list
                DestroyDeprecatedSpawnedObjects();                
            }
        }

        private void UpdateUserData()
        {
            // Compile user data
            OdinNetworkWriter userData = CompileUserData();

            // Compare if things have changed, then send an update
            if (!userData.IsEqual(_lastUserData))
            {
                Debug.Log($"Sending user data update: {userData.Cursor}");
                OdinNetworkManager.Instance.UpdateRoomData(userData);
            }

            // Store last user data
            _lastUserData = userData;
            _lastSent = Time.time;
        }
    }
}