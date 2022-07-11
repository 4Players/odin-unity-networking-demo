using System.Collections.Generic;
using ElRaccoone.Tweens;
using OdinNative.Odin.Peer;
using UnityEngine;

namespace Odin.OdinNetworking
{
    public class OdinNetworkIdentity : OdinNetworkItem
    {
        public Peer Peer;

        [Header("Network Settings")]
        [Tooltip("Sync the transform as part of the peers user data")]
        [SerializeField] private bool SyncTransform = true;
        
        [Tooltip("Sync the animator")]
        [SerializeField] private bool SyncAnimator = true;

        [Tooltip("The number of seconds until the next update is sent")]
        public float SendInterval = 0.1f;
        
        private OdinNetworkWriter _lastUserData = null;
        private OdinNetworkWriter _lastNetworkedObjectUpdate = null;
        Dictionary<int, OdinNetworkWriter> _lastNetworkedObjectStates = new Dictionary<int, OdinNetworkWriter>();
        private float _lastSent;

        private Animator _animator;

        public List<OdinNetworkedObject> SpawnedObjects { get; } = new List<OdinNetworkedObject>();
        private byte _objectId = 0;

        public override void OnStartClient()
        {
            // Get Animator
            if (!_animator)
            {
                _animator = GetComponentInChildren<Animator>();    
            }
            
            // If this is not the local player, set rigid body to be kinetic (i.e. position and rotation is not part of
            // physics calculation
            if (!IsLocalPlayer())
            {
                foreach (var rb in GetComponentsInChildren<Rigidbody>())
                {
                    rb.isKinematic = true;
                }
            }
        }

        private void FixedUpdate()
        {
            if (!IsLocalPlayer())
            {
                return;
            }
            
            // Wait for the next slot for sending data
            if (Time.time - _lastSent > SendInterval)
            {
                // Compile user data
                OdinNetworkWriter userData = new OdinNetworkWriter();
                
                // Update Transform
                userData.Write(SyncTransform);
                if (SyncTransform)
                {
                    userData.Write(transform);
                }
                
                // Update Animator
                userData.Write(SyncAnimator && _animator);
                if (SyncAnimator && _animator)
                {
                    userData.Write(_animator.parameterCount);
                    foreach (var param in _animator.parameters)
                    {
                        if (param.type == AnimatorControllerParameterType.Bool)
                        {
                            userData.Write(OdinPrimitive.Bool);
                            userData.Write(_animator.GetBool(param.name));       
                        } 
                        else if (param.type == AnimatorControllerParameterType.Float)
                        {
                            userData.Write(OdinPrimitive.Float);
                            userData.Write(_animator.GetFloat(param.name));
                        }
                        else if (param.type == AnimatorControllerParameterType.Int)
                        {
                            userData.Write(OdinPrimitive.Integer);
                            userData.Write(_animator.GetInteger(param.name));
                        }
                    }
                }

                // Update Sync Vars
                WriteSyncVars(userData);
                
                // Write Networked objects
                userData.Write((byte)SpawnedObjects.Count);
                foreach (var networkedObject in SpawnedObjects)
                {
                    networkedObject.SerializeHeader(userData);
                    networkedObject.SerializeBody(userData);
                }                

                // Compare if things have changed, then send an update
                if (!userData.IsEqual(_lastUserData))
                {
                    Debug.Log($"Sending user data update: {userData.Cursor}");
                    OdinNetworkManager.Instance.SendUserDataUpdate(userData);
                }

                // Store last user data
                _lastUserData = userData;
                _lastSent = Time.time;
            }
        }

        public void MessageReceived(OdinNetworkIdentity sender, OdinNetworkReader reader)
        {
            OdinMessageType messageType = reader.ReadMessageType();

            if (messageType == OdinMessageType.UpdateSyncVar)
            {
                var syncVarName = reader.ReadString();
                var currentValue = reader.ReadObject();

                OdinSyncVarInfo syncInfo = _syncVars[syncVarName];
                syncInfo.FieldInfo.SetValue(this, currentValue);
            } 
            else if (messageType == OdinMessageType.SpawnPrefab)
            {
                OdinMessage message = new OdinMessage(OdinMessageType.SpawnPrefab);
                var prefabId = reader.ReadByte();
                var objectId = reader.ReadByte();
                var position = reader.ReadVector3();
                var rotation = reader.ReadQuaternion();
                OdinNetworkManager.Instance.SpawnPrefab(this, prefabId, objectId, position, rotation);
            } 
            else if (messageType == OdinMessageType.UpdateNetworkedObject)
            {
                int numberOfObjects = reader.ReadByte();
                for (int i = 0; i < numberOfObjects; i++)
                {
                    var networkId = reader.ReadByte();
                    var networkedObject = OdinNetworkManager.Instance.FindNetworkedObject(sender.Peer.Id, networkId);
                    if (networkedObject)
                    {
                        var (localPosition, localRotation, localScale) = reader.ReadTransform();
                        networkedObject.TweenLocalPosition(localPosition, SendInterval);
                        networkedObject.TweenLocalRotation(localRotation.eulerAngles, SendInterval);
                        networkedObject.TweenLocalScale(localScale, SendInterval);
                    }
                }
            }
        }

        public void UserDataUpdated(OdinNetworkReader reader)
        {
            // Read transform
            var hasTransform = reader.ReadBoolean();
            if (hasTransform)
            {
                var (localPosition, localRotation, localScale) = reader.ReadTransform();
                gameObject.TweenLocalPosition(localPosition, SendInterval);
                gameObject.TweenLocalRotation(localRotation.eulerAngles, SendInterval);
                gameObject.TweenLocalScale(localScale, SendInterval);
            }
            
            // Read Animator
            var hasAnimator = reader.ReadBoolean();
            if (hasAnimator)
            {
                _animator = GetComponent<Animator>();
                var numberOfParams = reader.ReadInt();
                for (int i = 0; i < numberOfParams; i++)
                {
                    var param = _animator.GetParameter(i);
                    OdinPrimitive primitive = reader.ReadPrimitiveType();
                    if (primitive == OdinPrimitive.Bool)
                    {
                        _animator.SetBool(param.name, reader.ReadBoolean());
                    } 
                    else if (primitive == OdinPrimitive.Float)
                    {
                        _animator.SetFloat(param.name, reader.ReadFloat());
                    }
                    else if (primitive == OdinPrimitive.Integer)
                    {
                        _animator.SetInteger(param.name, reader.ReadInt());
                    }
                }
            }
            
            // Sync Vars
            ReadSyncVars(reader);
            
            // Networked Objects
            PreparedSpawnedObjectsForUpdate();
            
            var numberOfNetworkedObjects = reader.ReadByte();
            for (var i = 0; i < numberOfNetworkedObjects; i++)
            {
                var (objectId, prefabId) = OdinNetworkedObject.DeserializeHeader(reader);
                var networkedObject = FindNetworkedObject(objectId);
                if (networkedObject)
                {
                    networkedObject.UpdateFromReader(reader, true);
                    networkedObject.IsUpdated = true;
                }
                else
                {
                    networkedObject = OdinNetworkManager.Instance.SpawnPrefab(this, prefabId, objectId, Vector3.zero, Quaternion.identity);
                    networkedObject.UpdateFromReader(reader, false);
                    networkedObject.IsUpdated = true;
                    SpawnedObjects.Add(networkedObject);
                }
            }
            
            // Walk through all spawned objects and remove those that were not in the update list
            DestroyDeprecatedSpawnedObjects();
        }

        private void DestroyDeprecatedSpawnedObjects()
        {
            foreach (var spawnedObject in SpawnedObjects.ToArray())
            {
                if (spawnedObject.IsUpdated == false)
                {
                    DestroyNetworkedObject(spawnedObject);
                }
            }
        }

        private void PreparedSpawnedObjectsForUpdate()
        {
            foreach (var spawnedObject in SpawnedObjects)
            {
                spawnedObject.IsUpdated = false;
            }
        }

        public OdinNetworkedObject FindNetworkedObject(byte objectId)
        {
            foreach (var networkedObject in SpawnedObjects)
            {
                if (networkedObject.ObjectId == objectId)
                {
                    return networkedObject;
                }
            }

            return null;
        }
        
        public bool IsLocalPlayer()
        {
            return OdinNetworkManager.Instance.LocalPlayer == this;
        }

        public void SpawnManagedNetworkedObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            SpawnManagedNetworkedObject(prefab.name, position, rotation);
        }
        
        
        
        public void SpawnManagedNetworkedObject(byte prefabId, Vector3 position, Quaternion rotation)
        {
            var networkedObject = OdinNetworkManager.Instance.SpawnPrefab(this, prefabId, _objectId, position, rotation);
            if (networkedObject == null)
            {
                Debug.LogWarning($"Could not spawn prefab with id {prefabId}");
                return;
            }
            
            SpawnedObjects.Add(networkedObject);
            _objectId++;
        }

        public void SpawnManagedNetworkedObject(string prefabName, Vector3 position, Quaternion rotation)
        {
            var networkedObject = OdinNetworkManager.Instance.SpawnPrefab(this, prefabName, _objectId, position, rotation);
            if (networkedObject == null)
            {
                Debug.LogWarning($"Could not spawn prefab {prefabName}");
                return;
            }
            
            SpawnedObjects.Add(networkedObject);
            _objectId++;
        }

        public void SpawnNetworkedObject(string prefabName, Vector3 position, Quaternion rotation)
        {
            var networkedObject = OdinNetworkManager.Instance.SpawnPrefab(this, prefabName, _objectId, position, rotation);
            if (networkedObject == null)
            {
                Debug.LogWarning($"Could not spawn prefab {prefabName}");
                return;
            }
            
            OdinMessage message = new OdinMessage(OdinMessageType.SpawnPrefab);
            message.Write(networkedObject.PrefabId);
            message.Write(_objectId);
            message.Write(position);
            message.Write(rotation);
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

            SpawnedObjects.Remove(networkedObject);
            DestroyImmediate(networkedObject.gameObject);
        }
    }
}
