using System.Linq;
using ElRaccoone.Tweens;
using Odin.OdinNetworking.Messages;
using OdinNative.Odin.Peer;
using OdinNative.Odin.Room;
using OdinNative.Unity.Audio;
using UnityEngine;

namespace Odin.OdinNetworking
{
    /// <summary>
    /// This class represents a player/avatar in the world and wraps an <see cref="OdinNative.Odin.Peer"/>. It enables
    /// automatic synchronization of the players position, animation and sync vars over the network, so that every
    /// player/avatar sees the same state of the world. As its derived from <see cref="Odin.OdinNetworking.OdinNetworkItem"/>
    /// players can also spawn objects in the world and control them until they are destroyed.
    /// </summary>
    public abstract class OdinNetworkIdentity : OdinNetworkItem
    {
        /// <summary>
        /// The peer connection to the Odin server for this player/avatar. It's set by the <see cref="Odin.OdinNetworking.OdinNetworkManager"/>
        /// once the player object has been spawned.
        /// </summary>
        public Peer Peer;

        /// <summary>
        /// If enabled, the transform (i.e. position, rotation and scale) of this player/avatar will be sent to the
        /// network in the interval set in <see cref="Odin.OdinNetworking.OdinNetworkItem.SendInterval"/>. 
        /// </summary>
        [Header("Network Settings")]
        [Tooltip("Sync the transform as part of the peers user data")]
        [SerializeField] public bool SyncTransform = true;
        
        /// <summary>
        /// If enabled, the state of the animation is sent over the network so that every player connected will see
        /// the same animation sequence and state. 
        /// </summary>
        [Tooltip("Sync the animator")]
        [SerializeField] public bool SyncAnimator = true;

        /// <summary>
        /// Set the Animator object that should be used when syncing the animation. If this is not set, the script will
        /// take the first Animator object found in the game object or its hierarchy once the player object is spawned.
        /// </summary>
        [Tooltip("The animator used to sync animation if SyncAnimator is true. If not set the script will search for it in the hierarchy.")]
        public Animator _animator;
        
        /// <summary>
        /// Called in the first frame before the standard Unity Start method and sets up some default values.
        /// </summary>
        /// <remarks>Please call the base class if you override this function so that basic setup will be done.</remarks>
        public override void OnStartClient()
        {
            // Get Animator
            if (!_animator)
            {
                _animator = GetComponentInChildren<Animator>();    
            }
            
            // If this is not the local player, set rigid body to be kinetic (i.e. position and rotation is not part of
            // physics calculation
            if (!IsLocalPlayer)
            {
                IsKinetic = true;
            }
        }

        /// <summary>
        /// Checks if its time to send the next update and if this is the local player compiles an update package and
        /// sends it to the Odin server. All other identities in the network receive this package in their
        /// <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnUpdatedFromNetwork"/> function where they update their
        /// representation of this object.
        /// </summary>
        private void FixedUpdate()
        {
            // Don't do anything if this object is not the local player
            if (!IsLocalPlayer)
            {
                return;
            }
            
            // Wait for the next slot for sending data
            if (Time.time - _lastSent > SendInterval)
            {
                UpdateUserData();
            }
        }

        /// <summary>
        /// Compiles an <see cref="Odin.OdinNetworking.Messages.OdinUserDataUpdateMessage"/> object that contains
        /// all relevant information of this identity that should be synced over the network. The message itself will
        /// be packaged together by the <see cref="Odin.OdinNetworking.OdinNetworkWriter"/> and is sent over the network.
        /// </summary>
        /// <returns>An instance of a writer object that contains the packaged byte array that can be sent over the network</returns>
        private OdinUserDataUpdateMessage CompileUserData()
        {
            // Prepare the message object. We could write directly to a OdinNetworkWriter as it seems to be overkill
            // to first setting values in a class and using that class to write to the OdinNetworkWriter. Background
            // is that serialization of these kind of data requires tightly package byte sequence. The order is important
            // and whenever something changes the read and write part must be adjusted. Debugging this stuff is hell and
            // it's easier to find errors and it's easier to extend the system if we abstract the general data structure
            // from the actual package sent over the network
            OdinUserDataUpdateMessage message = new OdinUserDataUpdateMessage();

            // Serialize the transform (i.e. position, scale, and rotation) if SyncTransform is set
            message.HasTransform = SyncTransform;
            if (SyncTransform)
            {
                message.Transform = new OdinUserDataTransform(transform.localPosition, transform.localRotation,
                    transform.localScale);
            }

            // Serialite animation by storing the current animation parameters (that drive the actual animation).
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

            // Serialize the sync vars
            message.SyncVars = CompileSyncVars();
            
            // Serialize all managed objects, i.e. all objects that this identity has authority (i.e. objects it has
            // spawned or received control from another player/avatar)
            foreach (var networkedObject in ManagedObjects)
            {
                var transform = new OdinUserDataTransform(networkedObject.transform.localPosition, networkedObject.transform.localRotation, networkedObject.transform.localScale);
                var managedObject =
                    new OdinUserDataManagedObject(networkedObject.ObjectId, networkedObject.PrefabId, transform);
                managedObject.SyncVars = networkedObject.CompileSyncVars();
                message.ManagedObjects.Add(managedObject);
            }

            // It this identity is host, package all networked world objects, too.
            message.IsHost = IsHost;
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

            // Package everything together and return the writer object that compiles everything into a compressed
            // and optimized package.
            return message;
        }

        /// <summary>
        /// Compiles the data required in this update cycle and either sends the data as room user data if this identity
        /// is the current host or sends data as a peer user data update otherwise.
        /// </summary>
        private void UpdateUserData()
        {
            // Compile user data
            OdinUserDataUpdateMessage userDataMessage = CompileUserData();
            OdinNetworkWriter userData = userDataMessage.GetWriter();

            // Compare if things have changed, then send an update
            if (!userData.IsEqual(_lastUserData))
            {
                if (IsHost)
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

        /// <summary>
        /// Called from the <see cref="Odin.OdinNetworking.OdinNetworkManager"/> whenever a message is received for this
        /// identity. This function can be overriden by base classes to implement custom messages. 
        /// </summary>
        /// <remarks>Call the base class if you want to keep standard functionality like spawning prefabs over the network</remarks>
        /// <param name="sender">The identity that sent this message</param>
        /// <param name="message">The message object containing the data. You can use <see cref="Odin.OdinNetworking.Messages.OdinMessage.MessageType"/>
        /// </param>
        public virtual void OnMessageReceived(OdinNetworkIdentity sender, OdinMessage message)
        {
            if (message.MessageType == OdinMessageType.SpawnPrefab)
            {
                var spawnPrefabMessage = (OdinSpawnPrefabMessage)message;
                var prefabId = spawnPrefabMessage.PrefabId;
                var objectId = spawnPrefabMessage.ObjectId;
                var position = spawnPrefabMessage.Position;
                var rotation = spawnPrefabMessage.Rotation;
                OdinNetworkManager.Instance.SpawnPrefab(this, prefabId, objectId, position, rotation);
            }
        }

        /// <summary>
        /// Commands are special message that are only processed by the host. Other identities send a command to the host
        /// that will change the world accordingly and will then update to the world. If every identity could change
        /// the world it would often come to collisions and merge conflicts. Therefore only one player in the world
        /// is the host and responsible for updating the world. Other players in the world send their wish list of changes
        /// to as a command, which is processed by the host. This makes sure everyone sees the world in the same state.
        /// </summary>
        /// <remarks>You should override this function and implement your own commands. Make sure to check if you are the
        /// host with the <see cref="Odin.OdinNetworking.OdinNetworkIdentity.IsHost"/></remarks> property.
        /// <param name="message">The command message received</param>
        public abstract void OnCommandReceived(OdinCommandMessage message);

        /// <summary>
        /// Called from the <see cref="Odin.OdinNetworking.OdinNetworkManager"/> whenever a new
        /// <see cref="Odin.OdinNetworking.Messages.OdinUserDataUpdateMessage"/> has been received for this identity.
        /// This function interpolates the transform, the animation state and sync vars to the values sent by the server.
        /// In addition to that it also updates any managed objects, i.e. objects that this identity has authority and
        /// if this is the host, it also updates world objects.
        /// </summary>
        /// <param name="message">The update message containing the state of the identity on the server.</param>
        public virtual void OnUpdatedFromNetwork(OdinUserDataUpdateMessage message)
        {
            // Update the transform if available in the package by tweening the position to the latest values.
            if (message.HasTransform)
            {
                gameObject.TweenLocalPosition(message.Transform.Position, SendInterval);
                gameObject.TweenLocalRotation(message.Transform.Rotation.eulerAngles, SendInterval);
                gameObject.TweenLocalScale(message.Transform.Scale, SendInterval);
            }

            // Update the animation by setting the animation parameters received from the server
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

            // Update the sync vars. If the value has changed, call the hook function if provided by the user
            ReadSyncVars(message.SyncVars);
            
            // Update networked Objects (i.e. objects that this identity has authority)
            if (message.ManagedObjects.Count > 0)
            {
                // Mark all objects as not updated
                PreparedSpawnedObjectsForUpdate();
            
                // Work through the list and mark every object as updated which has been part of this message
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

            // If this message came from the host, update the world with the current state.
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
                        // Seems to be a new World Object - should not happen
                    }
                }
            }
        }

        /// <summary>
        /// Find a managed object by its ObjectId that this identity has been spawned previously.
        /// </summary>
        /// <remarks>Every networked object in the world has an identity as its owner. Each object spawned by
        /// the owner receives a unique ObjectId. Every networked object in the world can be found by the combination of
        /// unique PeerId and ObjectId (of that peer).
        /// </remarks>
        /// <param name="objectId">The object id of this object within the number space of its owner.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Used to get the game object where the speaker of the players audio out (i.e. his microphone) should be
        /// attached to. This game object will be used in the <see cref="OdinHandler.AddPlaybackComponent"/>. Think of
        /// this game object as the mouth of the player. The default implementation returns this game objects transform.
        /// </summary>
        /// <remarks>Please note: One player connect to multiple rooms, i.e. one for the 3D audio and another room
        /// for walkie-talkie like audio. Therefore, this function gives you the room where the microphone media has been
        /// added as you might have different places for the audio on your avatar.</remarks>
        /// <param name="room">The room where the microphone has been added. Use the RoomId to get the name of the room.</param>
        /// <returns>The transform that the AudioSource (i.e. Speaker) of the players audio out should be attached.</returns>
        public virtual GameObject GetPlaybackComponentContainer(Room room)
        {
            return gameObject;
        }

        /// <summary>
        /// Called once the <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> has been attached to game object
        /// served in the <see cref="Odin.OdinNetworking.OdinNetworkIdentity.GetPlaybackComponentContainer"/> callback
        /// function. Use it to adjust the settings of the <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> or
        /// it's AudioSource like spatialBlend to activate 3D positional audio.
        /// </summary>
        /// <param name="playbackComponent">An instance of the <see cref="OdinNative.Unity.Audio.PlaybackComponent"/>
        /// that has been attached to the players "mouth".</param>
        public virtual void OnPlaybackComponentCreated(PlaybackComponent playbackComponent)
        {
            // Activate 3D spatial audio
            playbackComponent.PlaybackSource.spatialBlend = 1.0f;
        }

        /// <summary>
        /// Called once the <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> has been destroyed. This happens if
        /// the player has muted his audio or he left the room.
        /// </summary>
        /// <remarks>The default implementation does nothing.</remarks>
        /// <param name="playbackComponent">The instance of the <see cref="OdinNative.Unity.Audio.PlaybackComponent"/>
        /// that is about to be destroyed.</param>
        public virtual void OnPlaybackComponentWillBeDestroyed(PlaybackComponent playbackComponent)
        {
            
        }

        /// <summary>
        /// Called from the <see cref="Odin.OdinNetworking.OdinNetworkManager"/> whenever a media stream has been added
        /// to a room by another user. This typically happens if a new player connected or if a previously muted player
        /// has unmuted his mic.
        /// </summary>
        /// <remarks>The default implementation calls the <see cref="Odin.OdinNetworking.OdinNetworkIdentity.GetPlaybackComponentContainer"/>
        /// and attaches a <see cref="OdinNative.Unity.Audio.PlaybackComponent"/> by calling the
        /// <see cref="OdinHandler.AddPlaybackComponent"/> function. Then it calls the OnPlaybackComponentCreated callback
        /// function. You typically do not override this function, but you can do so to implement your own behaviour.</remarks>
        /// <param name="room">The room where the media has been added</param>
        /// <param name="mediaId">The id of the media stream</param>
        public virtual void OnMediaAdded(Room room, long mediaId)
        {
            var mediaContainer = GetPlaybackComponentContainer(room);
            var playbackComponent = OdinHandler.Instance.AddPlaybackComponent(mediaContainer.gameObject, room.Config.Name, Peer.Id, mediaId);
            OnPlaybackComponentCreated(playbackComponent);   
        }
        
        /// <summary>
        /// Called when the media has been removed. This happens if a player disconnects or mutes his microphone.
        /// </summary>
        /// <remarks>The default implementation calls the <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnPlaybackComponentWillBeDestroyed"/>
        /// function so that you can implement additional behaviour without overriding this function and then destroys
        /// the playback component.</remarks>
        /// <param name="room">The room where the media has been removed</param>
        /// <param name="mediaStreamId">The media stream which has been stopped</param>
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
