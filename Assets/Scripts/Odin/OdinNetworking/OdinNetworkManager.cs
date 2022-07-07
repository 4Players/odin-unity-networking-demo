using System.Threading;
using OdinNative.Odin;
using OdinNative.Odin.Peer;
using OdinNative.Odin.Room;
using RSG;
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
    
    public class OdinNetworkManager : MonoBehaviour
    {
        [Tooltip("The name of the room where players join per default.")]
        [SerializeField] private string roomName = "World";
        
        [Tooltip("This is the player prefab that will be instantiated for each player connecting to the same room")]
        [SerializeField] private OdinPlayer playerPrefab;

        private Room _room;
        public OdinPlayer LocalPlayer { get; private set; }
        
        public static OdinNetworkManager Instance { get; private set; }
        
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
            networkedObject.MessageReceived(reader);
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
            OdinPlayer player = Instantiate(prefab);
            player.Peer = peer;
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

        public void SendMessage(OdinNetworkWriter writer)
        {
            var bytes = writer.ToBytes();
            Debug.Log($"Sent Message with bytes {bytes.Length}");
            _room.BroadcastMessage(bytes, false);
        }
        
        private void SendUserDataUpdateWorker(object state)
        {
            var workerItem = state as OdinUpdateUserWorkItem;
            workerItem.room.UpdatePeerUserData(workerItem.userData);
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
    }
}