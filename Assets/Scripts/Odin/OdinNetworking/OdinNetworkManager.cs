using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        private Room _room;
        public OdinPlayer LocalPlayer { get; private set; }
        
        public static OdinNetworkManager Instance { get; private set; }
        
        /// <summary>List of transforms populated by NetworkStartPositions</summary>
        public static List<Transform> startPositions = new List<Transform>();
        public static int startPositionIndex;
        

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
        }

        void Start()
        {
            OdinHandler.Instance.OnRoomJoined.AddListener(OnRoomJoined);
            OdinHandler.Instance.OnPeerJoined.AddListener(OnPeerJoined);
            OdinHandler.Instance.OnPeerLeft.AddListener(OnPeerLeft);
            OdinHandler.Instance.OnMessageReceived.AddListener(OnMessageReceived);
            OdinHandler.Instance.OnPeerUserDataChanged.AddListener(OnPeerUserDataUpdated);
            
            OdinHandler.Instance.JoinRoom(roomName);
        }

        private void OnDestroy()
        {
            OdinHandler.Instance.OnRoomJoined.RemoveListener(OnRoomJoined);
            OdinHandler.Instance.OnPeerJoined.RemoveListener(OnPeerJoined);
            OdinHandler.Instance.OnPeerLeft.RemoveListener(OnPeerLeft);
            OdinHandler.Instance.OnMessageReceived.RemoveListener(OnMessageReceived);
            OdinHandler.Instance.OnPeerUserDataChanged.RemoveListener(OnPeerUserDataUpdated);
            
            startPositionIndex = 0;
        }

        void Update()
        {

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
                // The message came from this peer
                OdinNetworkReader reader = new OdinNetworkReader(eventArgs.UserData);
                networkedObject.UserDataUpdated(reader);
            }
        }

        public OdinNetworkedObject FindNetworkedObject(ulong peerId, byte networkId)
        {
            foreach (var networkedObject in FindObjectsOfType<OdinNetworkedObject>())
            {
                if (networkedObject.Owner.Peer.Id == peerId && networkedObject.ObjectId == networkId)
                {
                    return networkedObject;
                }
            }

            return null;
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
        
        public virtual OdinPlayer AddPlayer(Peer peer, OdinPlayer prefab)
        {
            Transform startPos = GetStartPosition();
            OdinPlayer player = startPos != null ? Instantiate(prefab, startPos.position, startPos.rotation) : Instantiate(prefab);
            player.Peer = peer;
            player.OnAwakeClient();
            return player;
        }

        public virtual void OnClientConnected(Peer peer)
        {
            var player = AddPlayer(peer, playerPrefab);
            if (!peer.UserData.IsEmpty())
            {
                OdinNetworkReader reader = new OdinNetworkReader(peer.UserData);
                player.UserDataUpdated(reader);
            }
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
            LocalPlayer = AddPlayer(room.Self, playerPrefab);
            LocalPlayer.OnStartClient();
            LocalPlayer.OnStartLocalClient();
        }

        public void SendMessage(OdinNetworkWriter writer, bool includeSelf = false)
        {
            var workerItem = new OdinUpdateUserWorkItem();
            workerItem.room = _room;
            workerItem.userData = writer;
            ThreadPool.QueueUserWorkItem(SendMessageWorker, workerItem);
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

        public virtual OdinNetworkedObject SpawnPrefab(OdinNetworkIdentity owner, byte prefabId, byte objectId, Vector3 position, Quaternion rotation)
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
                foreach (var rb in obj.gameObject.GetComponentsInChildren<Rigidbody>())
                {
                    rb.isKinematic = true;
                }    
            }
            
            obj.OnStartClient();
            if (owner.IsLocalPlayer())
            {
                obj.OnStartLocalClient();
            }

            return obj;
        }

        public virtual OdinNetworkedObject SpawnPrefab(OdinNetworkIdentity owner, string prefabName, byte objectId, Vector3 position, Quaternion rotation)
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