using Odin.OdinNetworking.Messages;
using UnityEngine;

namespace Odin.OdinNetworking
{
    /// <summary>
    /// This class is a singleton that should only exist once per scene. Its derived from <see cref="Odin.OdinNetworking.OdinNetworkItem"/>
    /// and therefore gains the feature of managed objects and sync vars. It defines the networked world. The host will
    /// handle the worlds state and make sure it's synced with other players.
    /// You can create object at design time and add a <see cref="Odin.OdinNetworking.OdinNetworkedObject"/> component
    /// to them and they will automatically be part of the managed objects of the world which state will be synced by the
    /// host.
    /// </summary>
    public class OdinWorld : OdinNetworkItem
    {
        public static OdinWorld Instance { get; private set; }

        /// <summary>
        /// Return a networked object that is controlled by the world (i.e. an object that has been there at design time
        /// or spawned later)
        /// </summary>
        /// <param name="objectId">The object id for which to find an object</param>
        /// <returns>The networked object or null if not found</returns>
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

        /// <summary>
        /// Get a networked object instance for the provided game object
        /// </summary>
        /// <param name="go">The game object that should be found in the managed objects list.</param>
        /// <returns>The found object or null if nothing has been found.</returns>
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
        
        /// <summary>
        /// Creates a singleton instance and adds all networked objects that are available in the scene (i.e. game objects
        /// that have the <see cref="Odin.OdinNetworking.OdinNetworkedObject"/> script attached at design time. They will
        /// become part of the static networked world and will be synced by the host.
        /// </summary>
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
        
        /******************
         * The idea has been, that the world object synchronizes it's state itself as room data. But the bandwith is too
         * high if the host peer sends its own data as a peer update and the world as room update. Therefore the host
         * now sends its data as the room data with packaged world data.
         *
         * Another solution would be that the host creates another connection to the server and uses that to update the
         * world.
         */
        
        private void FixedUpdate()
        {
            return;
            
            if (!IsHost)
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