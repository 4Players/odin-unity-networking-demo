using System.Linq;
using ElRaccoone.Tweens;
using Odin.OdinNetworking.Messages;
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

        [Tooltip("The animator used to sync animation if SyncAnimator is true. If not set the script will search for it in the hierarchy.")]
        public Animator _animator;
        
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
                IsKinetic = true;
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
            
            foreach (var networkedObject in ManagedObjects)
            {
                var transform = new OdinUserDataTransform(networkedObject.transform.localPosition, networkedObject.transform.localRotation, networkedObject.transform.localScale);
                var managedObject =
                    new OdinUserDataManagedObject(networkedObject.ObjectId, networkedObject.PrefabId, transform);
                managedObject.SyncVars = networkedObject.CompileSyncVars();
                message.ManagedObjects.Add(managedObject);
            }

            message.IsHost = IsHost();
            if (message.IsHost)
            {
                message.WorldSyncVars = OdinWorld.Instance.CompileSyncVars();
                
                foreach (var networkedObject in OdinWorld.Instance.ManagedObjects)
                {
                    var transform = new OdinUserDataTransform(networkedObject.transform.localPosition, networkedObject.transform.localRotation, networkedObject.transform.localScale);
                    var managedObject =
                        new OdinUserDataManagedObject(networkedObject.ObjectId, networkedObject.PrefabId, transform);
                    managedObject.SyncVars = networkedObject.CompileSyncVars();
                    message.ManagedWorldObjects.Add(managedObject);
                }
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
                if (IsHost())
                {
                    OdinNetworkManager.Instance.UpdateRoomData(userData);
                }
                else
                {
                    OdinNetworkManager.Instance.SendUserDataUpdate(userData);                    
                }
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
            } else if (message.MessageType == OdinMessageType.Command)
            {
                OnCommandReceived((OdinCommandMessage)message);    
            }
        }

        public virtual void OnCommandReceived(OdinCommandMessage message)
        {
            
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
                        ManagedObjects.Add(networkedObject);
                    }
                }
            
                // Walk through all spawned objects and remove those that were not in the update list
                DestroyDeprecatedSpawnedObjects();                
            }

            if (message.IsHost)
            {
                OdinWorld.Instance.ReadSyncVars(message.WorldSyncVars);
                
                foreach (var managedObject in message.ManagedWorldObjects)
                {
                    var networkedObject = OdinWorld.Instance.GetNetworkObject(managedObject.ObjectId);
                    if (networkedObject)
                    {
                        networkedObject.OnUpdatedFromNetwork(managedObject);
                    }
                    else
                    {
                        // Seems to be a new World Object
                    }
                }
            }
        }

        public OdinNetworkedObject FindNetworkedObject(byte objectId)
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
