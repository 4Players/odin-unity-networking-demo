using System;
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
    public struct OdinUpdateUserDataJob : IJob
    {
        public OdinNetworkWriter writer;
        public Room room;
        
        public NativeArray<bool> result;

        public void Execute()
        {
            result[0] = room.UpdatePeerUserData(writer);
        }
    }

    public class OdinUpdateUserWorkItem
    {
        public IUserData userData;
        public Room room;
    }
    
    public enum OdinPlayerSpawnMethod { Random, RoundRobin }
    
    public class OdinNetworkManager : MonoBehaviour
    {
        [Tooltip("The name of the room where players join per default.")]
        [SerializeField] private string roomName = "World";
        
        [Header("Player spawning")]
        [Tooltip("This is the player prefab that will be instantiated for each player connecting to the same room")]
        [SerializeField] private OdinPlayer playerPrefab;
        
        [Tooltip("Round Robin or Random order of Start Position selection")]
        public OdinPlayerSpawnMethod playerSpawnMethod;

        [Header("Object spawning")]
        [Tooltip("Add prefabs that are spawnable in the network")]
        [SerializeField] private List<OdinNetworkedObject> spawnablePrefabs = new List<OdinNetworkedObject>();
        
        [Header("Voice Settings")] [SerializeField]
        [Tooltip("If enabled incoming media will be handled automatically and a PlaybackComponent will be attached to this game object.")]
        private bool handleMediaEvents = true;

        private Room _room;
        public OdinPlayer LocalPlayer { get; private set; }

        public static OdinNetworkManager Instance { get; private set; }
        
        /// <summary>List of transforms populated by NetworkStartPositions</summary>
        public static List<Transform> startPositions = new List<Transform>();
        public static int startPositionIndex;

        public OdinWorld World { get; private set;  }

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
            
            DontDestroyOnLoad(gameObject);

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

            if (handleMediaEvents)
            {
                OdinHandler.Instance.OnMediaAdded.AddListener(OnMediaAdded);
                OdinHandler.Instance.OnMediaRemoved.AddListener(OnMediaRemoved);                
            }
            
            OdinMessage message = GetJoinMessage();
            OdinHandler.Instance.JoinRoom(roomName, message);
        }

        private void OnDestroy()
        {
            OdinHandler.Instance.OnRoomJoined.RemoveListener(OnRoomJoined);
            OdinHandler.Instance.OnPeerJoined.RemoveListener(OnPeerJoined);
            OdinHandler.Instance.OnPeerLeft.RemoveListener(OnPeerLeft);
            OdinHandler.Instance.OnMessageReceived.RemoveListener(OnMessageReceived);
            OdinHandler.Instance.OnPeerUserDataChanged.RemoveListener(OnPeerUserDataUpdated);
            OdinHandler.Instance.OnRoomUserDataChanged.RemoveListener(OnRoomUserDataChanged);

            if (handleMediaEvents)
            {
                OdinHandler.Instance.OnMediaAdded.RemoveListener(OnMediaAdded);
                OdinHandler.Instance.OnMediaRemoved.RemoveListener(OnMediaRemoved);                
            }

            startPositionIndex = 0;
        }

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

        private void OnRoomUserDataChanged(object sender, RoomUserDataChangedEventArgs eventArgs)
        {
            Debug.Log($"Received room update data with length {eventArgs.Data.Buffer.Length}");
            
            HandleRoomUserData(eventArgs.Data);
        }
        
        private void OnMediaAdded(object sender, MediaAddedEventArgs eventArgs)
        {
            var room = sender as Room;
            if (room == null)
            {
                Debug.LogError($"OnMediaAdded sent not from a room: {sender.ToString()}");
                return;
            }
            
            var player = FindNetworkIdentityWithPeerId(eventArgs.Peer.Id);
            player.OnMediaAdded(room, eventArgs.Media.Id);
        }

        private void OnMediaRemoved(object sender, MediaRemovedEventArgs eventArgs)
        {
            var player = FindNetworkIdentityWithPeerId(eventArgs.Peer.Id);
            player.OnMediaRemoved(_room, eventArgs.MediaStreamId);
        }

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

        private void OnMessageReceived(object sender, MessageReceivedEventArgs eventArgs)
        {
            var networkedObject = FindNetworkIdentityWithPeerId(eventArgs.PeerId);
            if (networkedObject == null || networkedObject.IsLocalPlayer())
            {
                return;
            }
            
            // The message came from this peer
            OdinNetworkReader reader = new OdinNetworkReader(eventArgs.Data);
            networkedObject.MessageReceived(networkedObject, reader);
        }
        
        private void OnPeerLeft(object sender, PeerLeftEventArgs eventArgs)
        {
            OnClientDisconnected(eventArgs.PeerId);
        }
        
        private void OnPeerJoined(object sender, PeerJoinedEventArgs eventArgs)
        {
            OnClientConnected(eventArgs.Peer);
        }

        private void OnRoomJoined(RoomJoinedEventArgs eventArgs)
        {
            _room = eventArgs.Room;
            OnLocalClientConnected(eventArgs.Room);
        }
        
        public virtual OdinPlayer AddPlayer(Peer peer, OdinPlayer prefab, Vector3 position, Quaternion rotation)
        {
            OdinPlayer player = Instantiate(prefab, position, rotation);
            player.Peer = peer;
            player.OnAwakeClient();
            return player;
        }

        public virtual OdinMessage GetJoinMessage()
        {
            var startPos = GetStartPosition();
            var position = startPos == null ? Vector3.zero : startPos.position;
            var rotation = startPos == null ? Quaternion.identity : startPos.rotation;

            OdinUserDataUpdateMessage message = new OdinUserDataUpdateMessage();
            message.HasTransform = true;
            message.Transform = new OdinUserDataTransform(position, rotation, Vector3.one);
            return message;
        }

        public virtual void OnClientConnected(Peer peer)
        {
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            if (!peer.UserData.IsEmpty())
            {
                OdinMessage message = OdinMessage.FromBytes(peer.UserData);
                
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
            player.OnStartClient();
        }

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
        
        public virtual void OnLocalClientConnected(Room room)
        {
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            if (!room.Self.UserData.IsEmpty())
            {
                OdinUserDataUpdateMessage message = (OdinUserDataUpdateMessage)OdinMessage.FromBytes(room.Self.UserData);
                position = message.HasTransform ? message.Transform.Position : Vector3.zero;
                rotation = message.HasTransform ? message.Transform.Rotation : Quaternion.identity;    
            }
            
            LocalPlayer = AddPlayer(room.Self, playerPrefab, position, rotation);
            
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

        public void UpdateRoomData(OdinNetworkWriter writer)
        {
            if (_room == null)
            {
                return;
            }
            
            Debug.Log($"Updating Room Data: {writer.Cursor}");
            _room.UpdateRoomUserDataAsync(writer);
        }

        public void SendMessage(OdinNetworkWriter writer, bool includeSelf = false)
        {
            var workerItem = new OdinUpdateUserWorkItem();
            workerItem.room = _room;
            workerItem.userData = writer;
            ThreadPool.QueueUserWorkItem(SendMessageWorker, workerItem);
        }

        public void SendMessage(OdinMessage message, bool includeSelf = false)
        {
            var workerItem = new OdinUpdateUserWorkItem();
            workerItem.room = _room;
            workerItem.userData = message.GetWriter();
            ThreadPool.QueueUserWorkItem(SendMessageWorker, workerItem);
        }

        public bool IsHost()
        {
            if (_room == null)
            {
                return false;
            }
            
            return GetHost().Id == LocalPlayer.Peer.Id;
        }

        public OdinNetworkIdentity GetHostIdentity()
        {
            var hostPeer = GetHost();
            if (hostPeer == null) return null;

            return FindNetworkIdentityWithPeerId(hostPeer.Id);
        }

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

        public void SendCommand(OdinCommandMessage message)
        {
            var host = GetHost();
            if (host == null) return;
            
            _room.SendMessageAsync(new[]{ host.Id }, message.ToBytes());
        }
        
        private void SendUserDataUpdateWorker(object state)
        {
            var workerItem = state as OdinUpdateUserWorkItem;
            workerItem.room.UpdatePeerUserData(workerItem.userData);
        }

        private void SendMessageWorker(object state)
        {
            var workerItem = state as OdinUpdateUserWorkItem;
            workerItem.room.BroadcastMessage(workerItem.userData.ToBytes());
        }
/*
        private Promise<bool> SendUserDataWorker(Room room, IUserData userData)
        {
            var promise = new Promise<bool>();
            var result = room.UpdateUserData(userData);
            
        }
*/
        public void SendUserDataUpdate(OdinNetworkWriter writer)
        {
            //_room.UpdateUserData(writer);

            var workerItem = new OdinUpdateUserWorkItem();
            workerItem.room = _room;
            workerItem.userData = writer;
            ThreadPool.QueueUserWorkItem(SendUserDataUpdateWorker, workerItem);

        }

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
            if (!owner.IsLocalPlayer())
            {
                obj.IsKinetic = true;
            }
            
            obj.OnStartClient();
            if (owner.IsLocalPlayer())
            {
                obj.OnStartLocalClient();
            }

            return obj;
        }

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

        public static void RegisterSpawnPosition(Transform start)
        {
            // Debug.Log($"RegisterStartPosition: {start.gameObject.name} {start.position}");
            startPositions.Add(start);

            // reorder the list so that round-robin spawning uses the start positions
            // in hierarchy order.  This assumes all objects with NetworkStartPosition
            // component are siblings, either in the scene root or together as children
            // under a single parent in the scene.
            startPositions = startPositions.OrderBy(transform => transform.GetSiblingIndex()).ToList();
        }

        public static void UnregisterSpawnPosition(Transform start)
        {
            startPositions.Remove(start);
        }
        
        /// <summary>Get the next NetworkStartPosition based on the selected PlayerSpawnMethod.</summary>
        public Transform GetStartPosition()
        {
            // first remove any dead transforms
            startPositions.RemoveAll(t => t == null);

            if (startPositions.Count == 0)
                return null;

            if (playerSpawnMethod == OdinPlayerSpawnMethod.Random)
            {
                return startPositions[UnityEngine.Random.Range(0, startPositions.Count)];
            }
            else
            {
                Transform startPosition = startPositions[startPositionIndex];
                startPositionIndex = (startPositionIndex + 1) % startPositions.Count;
                return startPosition;
            }
        }
    }
}