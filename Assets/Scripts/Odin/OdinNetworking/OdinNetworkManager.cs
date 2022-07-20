using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Odin.OdinNetworking.Messages;
using OdinNative.Odin;
using OdinNative.Odin.Peer;
using OdinNative.Odin.Room;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Odin.OdinNetworking
{
    public class OdinUpdateUserWorkItem
    {
        public IUserData userData;
        public Room room;
    }

    /// <summary>
    /// Sets how spawn points should be handled. 
    /// </summary>
    public enum OdinPlayerSpawnMethod
    {
        /// <summary>
        /// Spawn points are selected randomly for each client.
        /// </summary>
        Random, 
        
        /// <summary>
        /// Spawn points are selected in order, i.e. the first clients gets the first spawn point, the second the second
        /// and so on. Once the last spawn point has been reached it starts over again with the first.
        /// </summary>
        RoundRobin
    }
    
    public class OdinNetworkManager : MonoBehaviour
    {
        /// <summary>
        /// The odin room name that the network manager should connect by default if autoConnect is set to true. 
        /// </summary>
        [Header("Room Settings")]
        [Tooltip("The name of the room where players join per default.")]
        [SerializeField] private string roomName = "World";
        
        /// <summary>
        /// If enabled the network manager will automatically connect to the room given in roomName on startup.
        /// </summary>
        /// <remarks>Use the <see cref="Odin.OdinNetworking.OdinNetworkManager.Connect"/> function to manually connect to a
        /// room whenever it's time to do so.</remarks>
        [Tooltip("Automatically connect to the room with default parameters. Otherwise you need to call the Connect function.")]
        [SerializeField] private bool autoConnect = false; 
        
        /// <summary>
        /// The prefab that should be spawned for each player connected to the room. 
        /// </summary>
        /// <remarks>In simpler games and experiences you often have once player prefab. You can customize the look & feel
        /// (i.e. color or different attachments) with sync vars.
        /// In more complex experiences where you have different prefabs per player. In this case, you need to create
        /// your own class derived from OdinNetworkManager and override the <see cref="Odin.OdinNetworking.OdinNetworkManager.OnClientConnected"/>
        /// or <see cref="Odin.OdinNetworking.OdinNetworkManager.AddPlayer"/> function. Use your own message when
        /// connecting to the room to transfer the character selection to all other clients.
        /// </remarks>
        [Header("Player spawning")]
        [Tooltip("This is the player prefab that will be instantiated for each player connecting to the same room")]
        [SerializeField] private OdinPlayer playerPrefab;
        
        /// <summary>
        /// Place <see cref="Odin.OdinNetworking.OdinNetworkSpawnPosition"/> objects in the scene to set spawn points
        /// where new players start when connecting to the room. Use this setting to set if the spawn point is selected
        /// randomly or in order one after the other.
        /// </summary>
        /// <remarks>You can make your own system for spawn points by creating your own class derived from the network
        /// manager and override the <see cref="Odin.OdinNetworking.OdinNetworkManager.GetStartPosition"/> function.</remarks>
        [Tooltip("Round Robin or Random order of Start Position selection")]
        public OdinPlayerSpawnMethod playerSpawnMethod;

        /// <summary>
        /// Make sure all prefabs that are spawned to the network are added to this list at design time. Only prefabs
        /// added to this list can be spawned at runtime into the network.
        /// </summary>
        /// <remarks>As strings require a lot of bytes over the network, prefabs are added to this list and if you spawn
        /// a prefab only the index in the list is sent over the network. It also makes sure that only allowed prefabs
        /// get spawned.</remarks>
        [Header("Object spawning")]
        [Tooltip("Add prefabs that are spawnable in the network")]
        [SerializeField] private List<OdinNetworkedObject> spawnablePrefabs = new List<OdinNetworkedObject>();
        
        /// <summary>
        /// Automatically handle media events, i.e. incoming media streams are automatically attached to player objects.
        /// In production you want this setting to be true. However, during development on a single machine you might
        /// get voice echos. If you are working on networking you can disable audio with this flag. 
        /// </summary>
        [Header("Voice Settings")] 
        [Tooltip("If enabled incoming media will be handled automatically and a PlaybackComponent will be attached to this game object.")]
        [SerializeField] private bool handleMediaEvents = true;

        /// <summary>
        /// The room the the manager is currently connected to.
        /// </summary>
        private Room _room;
        
        /// <summary>
        /// The local players identity object.
        /// </summary>
        public OdinPlayer LocalPlayer { get; private set; }

        /// <summary>
        /// The network manager is a singleton. Use this property to access the one and only instance of this class.
        /// </summary>
        public static OdinNetworkManager Instance { get; private set; }

        [Header("Lifetime Settings")] 
        [Tooltip("If enabled the singleton instance will survive scene changes, otherwise it will be destroyed when the scene changes.")]
        [SerializeField] private bool DontDestroyOnLoad = true;
        
        /// <summary>
        /// List of transforms populated by OdinNetworkSpawnPosition objects available in the scene.
        /// </summary>
        public static List<Transform> StartPositions = new List<Transform>();
        public static int StartPositionIndex;

        /// <summary>
        /// The OdinWorld instance available in the world. Set on Awake automatically
        /// </summary>
        public OdinWorld World { get; private set;  }

        /// <summary>
        /// A flag indicating if we are already connected to a room or not.
        /// </summary>
        public bool IsConnected => _room != null;
        
        
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }

            if (DontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);    
            }

            World = FindObjectOfType<OdinWorld>();
        }

        void Start()
        {
            OdinHandler.Instance.OnRoomJoined.AddListener(OnRoomJoined);
            OdinHandler.Instance.OnPeerJoined.AddListener(OnPeerJoined);
            OdinHandler.Instance.OnPeerLeft.AddListener(OnPeerLeft);
            OdinHandler.Instance.OnMessageReceived.AddListener(OnMessageReceived);
            OdinHandler.Instance.OnPeerUserDataChanged.AddListener(OnPeerUserDataUpdated);
            OdinHandler.Instance.OnRoomUserDataChanged.AddListener(OnRoomUserDataChanged);
            OdinHandler.Instance.OnMediaAdded.AddListener(OnMediaAdded);
            OdinHandler.Instance.OnMediaRemoved.AddListener(OnMediaRemoved);                

            if (autoConnect)
            {
                Connect();
            }
        }

        private void OnDestroy()
        {
            OdinHandler.Instance.OnRoomJoined.RemoveListener(OnRoomJoined);
            OdinHandler.Instance.OnPeerJoined.RemoveListener(OnPeerJoined);
            OdinHandler.Instance.OnPeerLeft.RemoveListener(OnPeerLeft);
            OdinHandler.Instance.OnMessageReceived.RemoveListener(OnMessageReceived);
            OdinHandler.Instance.OnPeerUserDataChanged.RemoveListener(OnPeerUserDataUpdated);
            OdinHandler.Instance.OnRoomUserDataChanged.RemoveListener(OnRoomUserDataChanged);
            OdinHandler.Instance.OnMediaAdded.RemoveListener(OnMediaAdded);
            OdinHandler.Instance.OnMediaRemoved.RemoveListener(OnMediaRemoved);                

            StartPositionIndex = 0;
        }

        /// <summary>
        /// Connect to the room provided by roomName and use the standard join message provided by
        /// <see cref="Odin.OdinNetworking.OdinNetworkManager.GetJoinMessage"/>.
        /// </summary>
        public void Connect()
        {
            OdinMessage message = GetJoinMessage();
            OdinHandler.Instance.JoinRoom(roomName, message);
        }

        /// <summary>
        /// Connect to a room given by the name. Uses the join message provided by
        /// <see cref="Odin.OdinNetworking.OdinNetworkManager.GetJoinMessage"/>.
        /// </summary>
        /// <param name="roomName">The name of the room to connect. Overrides the roomName given in the Inspector.</param>
        public void Connect(string roomName)
        {
            this.roomName = roomName;
            Connect();
        }

        /// <summary>
        /// Connect to the room set in the inspector and uses a custom join message. The join message is the standard
        /// message used to update the clients state. You can provide an initial state that the object should have, i.e.
        /// set sync values (like the color or name of the player) and you can even provide managed objects in the join
        /// message.
        /// </summary>
        /// <param name="message">A custom join message that defines the initial state of the player on all clients.</param>
        public void Connect(OdinUserDataUpdateMessage message)
        {
            OdinHandler.Instance.JoinRoom(roomName, message);
        }
        
        /// <summary>
        /// Connect to the room roomName and uses a custom join message. The join message is the standard
        /// message used to update the clients state. You can provide an initial state that the object should have, i.e.
        /// set sync values (like the color or name of the player) and you can even provide managed objects in the join
        /// message.
        /// </summary>
        /// <param name="roomName">The name of the room. Overrides the inspector setting.</param>
        /// <param name="message">A custom join message that defines the initial state of the player on all clients.</param>
        public void Connect(string roomName, OdinUserDataUpdateMessage message)
        {
            this.roomName = roomName;
            OdinHandler.Instance.JoinRoom(roomName, message);
        }

        /// <summary>
        /// Processes updated room data.
        /// </summary>
        /// <param name="roomUserData">The updated room data</param>
        private void HandleRoomUserData(byte[] roomUserData)
        {
            OdinMessage message = OdinMessage.FromBytes(roomUserData);
            // The message came from this peer
            if (message == null)
            {
                return;
            }

            if (message.MessageType == OdinMessageType.UserData)
            {
                Peer host = GetHost();
                var networkedObject = FindNetworkIdentityWithPeerId(host.Id);
                networkedObject.OnUpdatedFromNetwork((OdinUserDataUpdateMessage)message);   
            } 
            else if (message.MessageType == OdinMessageType.WorldUpdate)
            {
                OdinWorld.Instance.OnUpdatedFromNetwork((OdinWorldUpdateMessage)message);
            }
        }

        /// <summary>
        /// Handler for the RoomUserDataChanged Odin event
        /// </summary>
        /// <param name="sender">The sender of the message (a Room instance in this case)</param>
        /// <param name="eventArgs">The event arguments of the event</param>
        private void OnRoomUserDataChanged(object sender, RoomUserDataChangedEventArgs eventArgs)
        {
            Debug.Log($"Received room update data with length {eventArgs.Data.Buffer.Length}");
            
            HandleRoomUserData(eventArgs.Data);
        }
        
        /// <summary>
        /// Handler for the MediaAdded Odin event. Does nothing if <see cref="Odin.OdinNetworking.OdinNetworkManager.handleMediaEvents"/>
        /// is disabled.
        /// </summary>
        /// <remarks>It finds the network identity in the world and calls the <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnMediaAdded"/>
        /// function where a PlaybackComponent is created for this media object.
        /// </remarks>
        /// <param name="sender">The sender of the message (a Room instance in this case)</param>
        /// <param name="eventArgs">The event arguments of the event</param>
        private void OnMediaAdded(object sender, MediaAddedEventArgs eventArgs)
        {
            if (!handleMediaEvents) return;
            
            var room = sender as Room;
            if (room == null)
            {
                Debug.LogError($"OnMediaAdded sent not from a room: {sender.ToString()}");
                return;
            }
            
            var player = FindNetworkIdentityWithPeerId(eventArgs.Peer.Id);
            player.OnMediaAdded(room, eventArgs.Media.Id);
        }

        /// <summary>
        /// Handler for the MediaRemoved Odin event. Does nothing if <see cref="Odin.OdinNetworking.OdinNetworkManager.handleMediaEvents"/>
        /// is disabled.
        /// </summary>
        /// <remarks>It finds the corresponding network identity in the world and calls the <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnMediaRemoved"/>
        /// function where the PlaybackComponent is removed.
        /// </remarks>
        /// <param name="sender">The sender of the message (a Room instance in this case)</param>
        /// <param name="eventArgs">The event arguments of the event</param>
        private void OnMediaRemoved(object sender, MediaRemovedEventArgs eventArgs)
        {
            if (!handleMediaEvents) return;
            
            var player = FindNetworkIdentityWithPeerId(eventArgs.Peer.Id);
            player.OnMediaRemoved(_room, eventArgs.MediaStreamId);
        }

        /// <summary>
        /// Handler of the PeerUserDataUpdated Odin event. It creates an <see cref="Odin.OdinNetworking.Messages.OdinUserDataUpdateMessage"/> instance
        /// of the data received, finds the corresponding network identity in the scene and
        /// calls its <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnUpdatedFromNetwork"/> method that will update
        /// the network identities position, animation and sync vars.
        /// </summary>
        /// <param name="sender">The sender of the message (a Room instance in this case)</param>
        /// <param name="eventArgs">The event arguments of the event</param>
        private void OnPeerUserDataUpdated(object sender, PeerUserDataChangedEventArgs eventArgs)
        {
            Debug.Log($"Received update peer data with length {eventArgs.UserData.Buffer.Length}");
            if (eventArgs.Peer.Id == LocalPlayer.Peer.Id)
            {
                return;
            }

            if (eventArgs.UserData.IsEmpty())
            {
                return;
            }
            
            var networkedObject = FindNetworkIdentityWithPeerId(eventArgs.PeerId);
            if (networkedObject != null)
            {
                OdinUserDataUpdateMessage
                    message = (OdinUserDataUpdateMessage)OdinMessage.FromBytes(eventArgs.UserData);
                // The message came from this peer
                if (message.MessageType == OdinMessageType.UserData)
                {
                    networkedObject.OnUpdatedFromNetwork(message);   
                }
            }
        }

        /// <summary>
        /// Find a network identity based on its peer id. For each peer connected to the same Odin room an OdinNetworkIdentity
        /// instance is created in the scene which represents the player in the virtual world. 
        /// </summary>
        /// <param name="peerId">The unique peer id in the room.</param>
        /// <returns>null if no identity has been found in the scene or the <see cref="Odin.OdinNetworking.OdinNetworkIdentity"/>
        /// that has been found for this peer id.</returns>
        private OdinNetworkIdentity FindNetworkIdentityWithPeerId(ulong peerId)
        {
            foreach (var networkedObject in FindObjectsOfType<OdinNetworkIdentity>())
            {
                if (networkedObject.Peer.Id == peerId)
                {
                    return networkedObject;
                }
            }

            return null;
        }

        /// <summary>
        /// Handles MessageReceived Odin events. This function creates an <see cref="Odin.OdinNetworking.Messages.OdinMessage"/>
        /// instance (typically a subclass of OdinMessage) and either calls the <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnCommandReceived"/>
        /// if its a command (special type of message) or <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnMessageReceived"/>
        /// if its a message. 
        /// </summary>
        /// <remarks>Commands are only sent to the identity which is currently the host.</remarks>
        /// <param name="sender">The sender of the message (a Room instance in this case)</param>
        /// <param name="eventArgs">The event arguments of the event</param>
        private void OnMessageReceived(object sender, MessageReceivedEventArgs eventArgs)
        {
            var networkedObject = FindNetworkIdentityWithPeerId(eventArgs.PeerId);
            if (networkedObject == null || networkedObject.IsLocalPlayer)
            {
                return;
            }
            
            // The message came from this peer
            OdinNetworkReader reader = new OdinNetworkReader(eventArgs.Data);
            OdinMessage message = OdinMessage.FromReader(reader);
            if (message != null)
            {
                if (message.MessageType == OdinMessageType.Command)
                {
                    // Only send commands to the host
                    if (networkedObject.IsHost)
                    {
                        networkedObject.OnCommandReceived((OdinCommandMessage)message);
                    }   
                }
                else
                {
                    networkedObject.OnMessageReceived(networkedObject, message);    
                }
            }
        }
        
        /// <summary>
        /// Handler for PeerLeft Odin event. Calls the <see cref="Odin.OdinNetworking.OdinNetworkManager.OnClientDisconnected"/>
        /// function that you can override to customize its handling.
        /// </summary>
        /// <param name="sender">The sender of the message (a Room instance in this case)</param>
        /// <param name="eventArgs">The event arguments of the event</param>
        private void OnPeerLeft(object sender, PeerLeftEventArgs eventArgs)
        {
            OnClientDisconnected(eventArgs.PeerId);
        }
        
        /// <summary>
        /// Handler for PeerJoin Odin event. Calls the <see cref="Odin.OdinNetworking.OdinNetworkManager.OnClientConnected"/>
        /// function that you can override to customize its handling.
        /// </summary>
        /// <param name="sender">The sender of the message (a Room instance in this case)</param>
        /// <param name="eventArgs">The event arguments of the event</param>
        private void OnPeerJoined(object sender, PeerJoinedEventArgs eventArgs)
        {
            OnClientConnected(eventArgs.Peer);
        }

        /// <summary>
        /// Handler for RoomJoined Odin event. Calls the <see cref="Odin.OdinNetworking.OdinNetworkManager.OnLocalClientConnected"/>
        /// function that you can override to customize its handling.
        /// </summary>
        /// <param name="sender">The sender of the message (a Room instance in this case)</param>
        /// <param name="eventArgs">The event arguments of the event</param>
        private void OnRoomJoined(RoomJoinedEventArgs eventArgs)
        {
            _room = eventArgs.Room;
            OnLocalClientConnected(eventArgs.Room);
        }
        
        /// <summary>
        /// Called by the <see cref="Odin.OdinNetworking.OdinNetworkManager.OnClientConnected"/> function after a peer
        /// has joined the room. This function instantiates the prefab for the given peer and the given location
        /// and rotation in the scene.
        /// </summary>
        /// <param name="peer">The Odin peer for which a virtual avatar should be created in the scene</param>
        /// <param name="prefab">The prefab that should be instantiated. </param>
        /// <param name="position">The position where the new avatar should be placed in the scene</param>
        /// <param name="rotation">The initial rotation of the avatar</param>
        /// <returns>An instance of the created player object.</returns>
        public virtual OdinPlayer AddPlayer(Peer peer, OdinPlayer prefab, Vector3 position, Quaternion rotation)
        {
            OdinPlayer player = Instantiate(prefab, position, rotation);
            player.Peer = peer;
            player.OnAwakeClient();
            return player;
        }

        /// <summary>
        /// When connecting a room, the initial state of a user is provided, i.e. where to position the new avatar and
        /// what are the initial values of the sync var. If you want users to select an avatar, name and color, then you
        /// need to customize the initial user data object so that every client creates the same and correctly configured
        /// avatar to the scene. The default implementation will select the start position by spawn positions available
        /// in the scene depending on the spawn settings and will set the position of the avatar.
        /// </summary>
        /// <remarks>You can override this function in a derived class and customize it. But its easier to disable automatic
        /// room connection and use the <see cref="Odin.OdinNetworking.OdinNetworkManager.Connect"/> message that accepts
        /// a custom message object.</remarks>
        /// <returns>An instance of the initial user data object for the local player</returns>
        public virtual OdinUserDataUpdateMessage GetJoinMessage()
        {
            var startPos = GetStartPosition();
            var position = startPos == null ? Vector3.zero : startPos.position;
            var rotation = startPos == null ? Quaternion.identity : startPos.rotation;

            OdinUserDataUpdateMessage message = new OdinUserDataUpdateMessage();
            message.HasTransform = true;
            message.Transform = new OdinUserDataTransform(position, rotation, Vector3.one);
            return message;
        }

        /// <summary>
        /// Called whenever a new peer has joined the server. The default implementation will decode the message to get
        /// rotation and position of the avatar and will call the <see cref="Odin.OdinNetworking.OdinNetworkManager.AddPlayer"/>
        /// function to create an instance of the playerPrefab provided in the inspector.
        /// Next the created player object will be updated with the user data provided in the connect message (which might
        /// also contain initial managed objects and sync var values) and its <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnStartClient"/>
        /// function will be called
        /// </summary>
        /// <param name="peer">The Odin peer that just connected the server</param>
        public virtual void OnClientConnected(Peer peer)
        {
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            OdinMessage message = null;
            if (!peer.UserData.IsEmpty())
            {
                message = OdinMessage.FromBytes(peer.UserData);
                
                // Check if this user just joined in with a crippled JoinServer Message or a complete User Data object
                if (message.MessageType == OdinMessageType.UserData)
                {
                    var userDataMessage = (OdinUserDataUpdateMessage)message;
                    if (userDataMessage != null && userDataMessage.HasTransform)
                    {
                        position = userDataMessage.Transform.Position;
                        rotation = userDataMessage.Transform.Rotation;
                    } 
                }
                else
                {
                    Debug.LogError($"Unknown Message Type on client connection: {message.MessageType}");
                }
            }
            else
            {
                Debug.LogWarning($"Peer does not have any data {peer.Id}");
            }
            var player = AddPlayer(peer, playerPrefab, position, rotation);
            if (message != null && message.MessageType == OdinMessageType.UserData)
            {
                player.OnUpdatedFromNetwork((OdinUserDataUpdateMessage)message);
            }
            player.OnStartClient();
        }

        /// <summary>
        /// Called when a peer disconnected from the server. This default implementation finds the player in the
        /// scene and removes it from the scene. 
        /// </summary>
        /// <remarks>If its a remove peer the callback function <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnStopClient"/>
        /// is called. If its the local player the callback function <see cref="Odin.OdinNetworking.OdinNetworkManager.OnStopLocalClient"/>
        /// will be called after OnStopClient so that you can handle special handling for the local player.</remarks>
        /// <param name="peerId">The peer id that got removed from the server</param>
        public virtual void OnClientDisconnected(ulong peerId)
        {
            var networkedObject = FindNetworkIdentityWithPeerId(peerId);
            networkedObject.OnStopClient();
            if (networkedObject == LocalPlayer)
            {
                networkedObject.OnStopLocalClient();
            }
            
            DestroyImmediate(networkedObject.gameObject);
        }
        
        /// <summary>
        /// Called after the local player joined the room. This function does basically the same as OnClient Connected.
        /// It decodes the update message to get initial position and rotation, creates a player object using the
        /// <see cref="Odin.OdinNetworking.OdinNetworkManager.AddPlayer"/> function and calls the
        /// <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnStartClient"/> and after that the
        /// <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnStartLocalClient"/> function.
        /// </summary>
        /// <param name="room">The room which has been joined</param>
        public virtual void OnLocalClientConnected(Room room)
        {
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            OdinUserDataUpdateMessage message = null;
            if (!room.Self.UserData.IsEmpty())
            {
                message = (OdinUserDataUpdateMessage)OdinMessage.FromBytes(room.Self.UserData);
                position = message.HasTransform ? message.Transform.Position : Vector3.zero;
                rotation = message.HasTransform ? message.Transform.Rotation : Quaternion.identity;    
            }
            
            LocalPlayer = AddPlayer(room.Self, playerPrefab, position, rotation);
            if (message != null)
            {
                LocalPlayer.OnUpdatedFromNetwork(message);
            }
            /*
            else
            {
                // Just a client
                OdinWorldUpdateMessage message = (OdinWorldUpdateMessage)OdinMessage.FromBytes(room.);
                // The message came from this peer
                if (message.MessageType == OdinMessageType.UserData)
                {
                    World.OnUpdatedFromNetwork(message);   
                }
            }*/

            LocalPlayer.OnStartClient();
            LocalPlayer.OnStartLocalClient();
            
            // If we have room data, make sure to update the host
            if (!room.RoomUserData.IsEmpty())
            {
                HandleRoomUserData(room.RoomUserData);
            }
        }

        /// <summary>
        /// Update the room data with the bytes provided by the writer object.
        /// </summary>
        /// <remarks>Please note: This function can only be called from the host, as only the host may change the room
        /// data.</remarks>
        /// <param name="writer">The writer containing the byte sequence that should be the new room data</param>
        public void UpdateRoomData(OdinNetworkWriter writer)
        {
            if (!IsConnected) return;
            if (!IsHost()) return;

            Debug.Log($"Updating Room Data: {writer.Cursor}");
            _room.UpdateRoomUserDataAsync(writer);
        }

        /// <summary>
        /// Send a message to the server
        /// </summary>
        /// <param name="writer">The writer containing the bytes that should be sent</param>
        /// <param name="includeSelf"></param>
        private void SendMessage(OdinNetworkWriter writer, bool includeSelf = false)
        {
            _room.BroadcastMessageAsync(writer.ToBytes(), includeSelf);
        }

        /// <summary>
        /// Broadcast a message in the network to all connected clients. 
        /// </summary>
        /// <remarks>You can create your own messages. For this, create a subclass of <see cref="Odin.OdinNetworking.Messages.OdinMessage"/>
        /// and add the required properties that should be part of the message. Then implement the serialization of the
        /// message (see existing messages for reference). Override the <see cref="Odin.OdinNetworking.OdinNetworkIdentity.OnMessageReceived"/>
        /// and handle the logic for your custom message once received on client side.
        /// </remarks>
        /// <param name="message">The message that should be sent</param>
        /// <param name="includeSelf">Include yourself</param>
        public void SendMessage(OdinMessage message, bool includeSelf = false)
        {
            SendMessage(message.GetWriter(), includeSelf);
        }

        /// <summary>
        /// Returns true if the local player is the host. The host has responsibility for handling commands and to update
        /// the worlds state to other clients.
        /// </summary>
        /// <returns>True if the host, otherwise false.</returns>
        public bool IsHost()
        {
            if (!IsConnected) return false;
            
            return GetHost().Id == LocalPlayer.Peer.Id;
        }

        /// <summary>
        /// Return the identity of the current host
        /// </summary>
        /// <returns></returns>
        public OdinNetworkIdentity GetHostIdentity()
        {
            var hostPeer = GetHost();
            if (hostPeer == null) return null;

            return FindNetworkIdentityWithPeerId(hostPeer.Id);
        }

        /// <summary>
        /// Get the current host. Every client defines the same host based on a deterministic algorithm: The smallest peerid
        /// in the network is host. This way, no handshakes or data need to be transmitted over the network. Instead, every
        /// frame the current host is determined this way and all clients define the same host without exchanging data.
        /// </summary>
        /// <remarks>As the host is determined by the peer id it can happen, that after connect of another peer the host
        /// changes. This is intended behaviour!</remarks>
        /// <returns>The Peer of the current host.</returns>
        public Peer GetHost()
        {
            if (_room == null)
            {
                return null;
            }

            // TODO: This is the worst way to do it, but we need a deterministic way of figuring out a host
            // that is the same for all clients in the network without sending data around
            for (ulong i = 0; i < 1000; i++)
            {
                if (_room.Self.Id == i)
                {
                    return _room.Self;
                }

                Peer peer = _room.RemotePeers[i];
                if (peer != null)
                {
                    return peer;
                }
            }

            Debug.LogError("No peer found");
            return null;
        }

        /// <summary>
        /// Send a command to the current host which has to process the command. A command has a name and a list of parameters
        /// that can be set by the sender.
        /// </summary>
        /// <param name="message">The command message</param>
        public void SendCommand(OdinCommandMessage message)
        {
            // TODO: There are edge cases where this might not work, i.e. command is sent, and before the command is processed
            // the host changes.
            
            var host = GetHost();
            if (host == null) return;

            // Odin does not allow messages to be sent to yourself. So, if this is the host, fake the network response
            if (host.Id == LocalPlayer.Peer.Id)
            {
                LocalPlayer.OnCommandReceived(message);
                return;
            }            
            
            _room.SendMessageAsync(new[]{ host.Id }, message.ToBytes());
        }

        /// <summary>
        /// Update the local peers user data with the provided data.
        /// </summary>
        /// <param name="writer">The writer containing the bytes to be updated</param>
        public void SendUserDataUpdate(OdinNetworkWriter writer)
        {
            _room.UpdatePeerUserDataAsync(writer);
        }

        /// <summary>
        /// Spawn a networked object by its prefab id.  
        /// </summary>
        /// <param name="owner">The owner of the object</param>
        /// <param name="prefabId">The index of the prefab in the spawnablePrefabs list.</param>
        /// <param name="objectId">The objectid of the object.</param>
        /// <param name="position">The position of the object</param>
        /// <param name="rotation">The rotation of the object</param>
        /// <returns>The networked objects instance created in the network</returns>
        public virtual OdinNetworkedObject SpawnPrefab(OdinNetworkItem owner, byte prefabId, byte objectId, Vector3 position, Quaternion rotation)
        {
            if (prefabId >= spawnablePrefabs.Count)
            {
                Debug.LogError($"Could not prefab with id: {prefabId}");
                return null;
            }
            
            var prefab = spawnablePrefabs[prefabId];
            var obj = Instantiate(prefab, position, rotation);
            obj.Owner = owner;
            obj.ObjectId = objectId;
            obj.PrefabId = prefabId;
            obj.OnAwakeClient();

            // Make sure that rigid bodies are set to kinetic if they have been spawned by other clients (they control the position)
            if (!owner.IsLocalPlayer)
            {
                obj.IsKinetic = true;
            }
            
            obj.OnStartClient();
            if (owner.IsLocalPlayer)
            {
                obj.OnStartLocalClient();
            }

            return obj;
        }

        /// <summary>
        /// Spawn a networked object by its prefab name.  
        /// </summary>
        /// <param name="owner">The owner of the object</param>
        /// <param name="prefabName">The name of the prefab in the spawnablePrefabs list.</param>
        /// <param name="objectId">The objectid of the object.</param>
        /// <param name="position">The position of the object</param>
        /// <param name="rotation">The rotation of the object</param>
        /// <returns>The networked objects instance created in the network</returns>
        public virtual OdinNetworkedObject SpawnPrefab(OdinNetworkItem owner, string prefabName, byte objectId, Vector3 position, Quaternion rotation)
        {
            for (byte i=0;i<spawnablePrefabs.Count;i++)
            {
                var networkedObject = spawnablePrefabs[i];
                if (networkedObject.name == prefabName)
                {
                    return SpawnPrefab(owner, i, objectId, position, rotation);
                }   
            }
            
            Debug.LogError("Could not spawn prefab as its not in the list. Add the prefab to the OdinNetworkManager SpawnablePrefabs list");
            return null;
        }

        /// <summary>
        /// Called when the scene loads. Collects a list of <see cref="Odin.OdinNetworking.OdinNetworkSpawnPosition"/>
        /// objects distributed in the scene. 
        /// </summary>
        /// <param name="start"></param>
        public static void RegisterSpawnPosition(Transform start)
        {
            // Debug.Log($"RegisterStartPosition: {start.gameObject.name} {start.position}");
            StartPositions.Add(start);

            // reorder the list so that round-robin spawning uses the start positions
            // in hierarchy order.  This assumes all objects with NetworkStartPosition
            // component are siblings, either in the scene root or together as children
            // under a single parent in the scene.
            StartPositions = StartPositions.OrderBy(transform => transform.GetSiblingIndex()).ToList();
        }

        /// <summary>
        /// Remove a spawn position from the list.
        /// </summary>
        /// <param name="start"></param>
        public static void UnregisterSpawnPosition(Transform start)
        {
            StartPositions.Remove(start);
        }
        
        /// <summary>
        /// Get the next NetworkStartPosition based on the selected PlayerSpawnMethod.
        /// </summary>
        public virtual Transform GetStartPosition()
        {
            // first remove any dead transforms
            StartPositions.RemoveAll(t => t == null);

            if (StartPositions.Count == 0)
                return null;

            if (playerSpawnMethod == OdinPlayerSpawnMethod.Random)
            {
                return StartPositions[UnityEngine.Random.Range(0, StartPositions.Count)];
            }
            else
            {
                Transform startPosition = StartPositions[StartPositionIndex];
                StartPositionIndex = (StartPositionIndex + 1) % StartPositions.Count;
                return startPosition;
            }
        }
    }
}