using System.Collections.Generic;
using System.Linq;
using ElRaccoone.Tweens;
using OdinNative.Odin;
using OdinNative.Odin.Peer;
using OdinNative.Odin.Room;
using OdinNative.Unity.Audio;
using UnityEngine;

namespace Odin.OdinNetworking
{
    public class OdinNetworkIdentity : OdinNetworkItem
    {
        public Peer Peer;

        [Header("Network Settings")]
        [Tooltip("Sync the transform as part of the peers user data")]
        [SerializeField] public bool SyncTransform = true;
        
        [Tooltip("Sync the animator")]
        [SerializeField] public bool SyncAnimator = true;

        [Tooltip("The number of seconds until the next update is sent")]
        public float SendInterval = 0.1f;
        
        private OdinNetworkWriter _lastUserData = null;
        private OdinNetworkWriter _lastNetworkedObjectUpdate = null;
        Dictionary<int, OdinNetworkWriter> _lastNetworkedObjectStates = new Dictionary<int, OdinNetworkWriter>();
        private float _lastSent;

        public Animator _animator;

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
                UpdateUserData();
            }
        }

        private OdinNetworkWriter CompileUserData()
        {
            OdinUserDataUpdateMessage message = new OdinUserDataUpdateMessage();
            message.HasTransform = SyncTransform;
            if (SyncTransform)
            {
                message.Transform = new OdinUserDataTransform(transform.localPosition, transform.localRotation,
                    transform.localScale);
            }

            if (SyncAnimator && _animator)
            {
                message.HasAnimationParameters = true;
                foreach (var param in _animator.parameters)
                {
                    var animationParam = new OdinUserDataAnimationParam();
                    if (param.type == AnimatorControllerParameterType.Bool)
                    {
                        animationParam.Primitive = OdinPrimitive.Bool;
                        animationParam.Value = _animator.GetBool(param.name);
                    } 
                    else if (param.type == AnimatorControllerParameterType.Float)
                    {
                        animationParam.Primitive = OdinPrimitive.Float;
                        animationParam.Value = _animator.GetFloat(param.name);
                    }
                    else if (param.type == AnimatorControllerParameterType.Int)
                    {
                        animationParam.Primitive = OdinPrimitive.Integer;
                        animationParam.Value = _animator.GetInteger(param.name);
                    }

                    message.AnimationParams.Add(animationParam);
                }
            }

            message.SyncVars = CompileSyncVars();
            
            foreach (var networkedObject in SpawnedObjects)
            {
                var transform = new OdinUserDataTransform(networkedObject.transform.localPosition, networkedObject.transform.localRotation, networkedObject.transform.localScale);
                var managedObject =
                    new OdinUserDataManagedObject(networkedObject.ObjectId, networkedObject.PrefabId, transform);
                managedObject.SyncVars = networkedObject.CompileSyncVars();
                message.ManagedObjects.Add(managedObject);
            }

            return message.GetWriter();
        }

        private void UpdateUserData()
        {
            // Compile user data
            OdinNetworkWriter userData = CompileUserData();

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

        public void MessageReceived(OdinNetworkIdentity sender, OdinNetworkReader reader)
        {
            OdinMessage message = OdinMessage.FromReader(reader);
            if (message.MessageType == OdinMessageType.SpawnPrefab)
            {
                var spawnPrefabMessgage = (OdinSpawnPrefabMessage)message;
                var prefabId = spawnPrefabMessgage.PrefabId;
                var objectId = spawnPrefabMessgage.ObjectId;
                var position = spawnPrefabMessgage.Position;
                var rotation = spawnPrefabMessgage.Rotation;
                OdinNetworkManager.Instance.SpawnPrefab(this, prefabId, objectId, position, rotation);
            }
        }

        public virtual void OnUpdatedFromNetwork(OdinUserDataUpdateMessage message)
        {
            if (message.HasTransform)
            {
                gameObject.TweenLocalPosition(message.Transform.Position, SendInterval);
                gameObject.TweenLocalRotation(message.Transform.Rotation.eulerAngles, SendInterval);
                gameObject.TweenLocalScale(message.Transform.Scale, SendInterval);
            }

            if (message.HasAnimationParameters && _animator)
            {
                _animator = GetComponent<Animator>();
                for (int i = 0; i < message.AnimationParams.Count; i++)
                {
                    var param = _animator.GetParameter(i);
                    var animationParam = message.AnimationParams[i];
                    if (animationParam.Primitive == OdinPrimitive.Bool)
                    {
                        _animator.SetBool(param.name, (bool)animationParam.Value);
                    } 
                    else if (animationParam.Primitive == OdinPrimitive.Float)
                    {
                        _animator.SetFloat(param.name, (float)animationParam.Value);
                    }
                    else if (animationParam.Primitive == OdinPrimitive.Integer)
                    {
                        _animator.SetInteger(param.name, (int)animationParam.Value);
                    }
                }
            }

            ReadSyncVars(message.SyncVars);
            
            // Networked Objects
            if (message.ManagedObjects.Count > 0)
            {
                PreparedSpawnedObjectsForUpdate();
            
                foreach (var managedObject in message.ManagedObjects)
                {
                    var networkedObject = FindNetworkedObject(managedObject.ObjectId);
                    if (networkedObject)
                    {
                        networkedObject.OnUpdatedFromNetwork(managedObject, true);
                        networkedObject.IsUpdated = true;
                    }
                    else
                    {
                        networkedObject = OdinNetworkManager.Instance.SpawnPrefab(this, managedObject.PrefabId, managedObject.ObjectId, Vector3.zero, Quaternion.identity);
                        networkedObject.OnUpdatedFromNetwork(managedObject, false);
                        networkedObject.IsUpdated = true;
                        SpawnedObjects.Add(networkedObject);
                    }
                }
            
                // Walk through all spawned objects and remove those that were not in the update list
                DestroyDeprecatedSpawnedObjects();                
            }
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

        private void AddToSpawnedObjectsList(OdinNetworkedObject networkedObject)
        {
            SpawnedObjects.Add(networkedObject);
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
            
            AddToSpawnedObjectsList(networkedObject);
        }

        public void SpawnManagedNetworkedObject(string prefabName, Vector3 position, Quaternion rotation)
        {
            var networkedObject = OdinNetworkManager.Instance.SpawnPrefab(this, prefabName, _objectId, position, rotation);
            if (networkedObject == null)
            {
                Debug.LogWarning($"Could not spawn prefab {prefabName}");
                return;
            }
            
            AddToSpawnedObjectsList(networkedObject);
        }

        public void SpawnNetworkedObject(string prefabName, Vector3 position, Quaternion rotation)
        {
            var networkedObject = OdinNetworkManager.Instance.SpawnPrefab(this, prefabName, _objectId, position, rotation);
            if (networkedObject == null)
            {
                Debug.LogWarning($"Could not spawn prefab {prefabName}");
                return;
            }

            OdinSpawnPrefabMessage message = new OdinSpawnPrefabMessage(networkedObject.PrefabId, _objectId, position, rotation);
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

        public virtual Transform GetPlaybackComponentContainer(Room room)
        {
            return gameObject.transform;
        }

        public virtual void OnPlaybackComponentCreated(PlaybackComponent playbackComponent)
        {
            // Activate 3D spatial audio
            playbackComponent.PlaybackSource.spatialBlend = 1.0f;
        }

        public virtual void OnPlaybackComponentWillBeDestroyed(PlaybackComponent playbackComponent)
        {
            
        }

        public virtual void OnMediaAdded(Room room, long mediaId)
        {
            var mediaContainer = GetPlaybackComponentContainer(room);
            var playbackComponent = OdinHandler.Instance.AddPlaybackComponent(mediaContainer.gameObject, room.Config.Name, Peer.Id, mediaId);
            OnPlaybackComponentCreated(playbackComponent);   
        }
        
        public virtual void OnMediaRemoved(Room room, long mediaStreamId)
        {
            foreach (var playbackComponent in GetComponentsInChildren<PlaybackComponent>().ToArray())
            {
                if (playbackComponent.MediaStreamId == mediaStreamId)
                {
                    OnPlaybackComponentWillBeDestroyed(playbackComponent);
                    Destroy(playbackComponent);
                }
            }
        }
    }
}
