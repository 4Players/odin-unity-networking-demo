using Odin.OdinNetworking.Messages;
using OdinNative.Odin.Room;
using OdinNetworking;
using UnityEngine;

namespace Odin.OdinNetworking
{
    public class OdinPlayer : OdinNetworkIdentity
    {
        [Header("Player Settings")]
        [Tooltip("Set an OdinEars instance. It's where the AudioListener and special AudioEffects are handled. If not set, it will seek in the hierarchy for it.")]
        public OdinEars odinEars;
        
        [Tooltip("Set an OdinMouth instance. It's where the Audio output is positioned. Position that object near the mouth of the avatar.")]
        public OdinMouth odinMouth;

        [OdinSyncVar(hook = nameof(OnNameChanged))]
        [Tooltip("The name of the player that is used by default.")]
        public string Name;

        protected void OnEnable()
        {
            if (!odinEars)
            {
                odinEars = GetComponentInChildren<OdinEars>(true);
            }

            if (!odinMouth)
            {
                odinMouth = GetComponentInChildren<OdinMouth>(true);
            }

            // Disable the ears (i.e. AudioListener) - we later enable it for the local player in OnLocalClientConnected
            if (odinEars)
            {
                odinEars.gameObject.SetActive(false);
            }   
        }
        
        public override Transform GetPlaybackComponentContainer(Room room)
        {
            return odinMouth != null ? odinMouth.transform : gameObject.transform;
        }

        public override void OnCommandReceived(OdinCommandMessage message)
        {
            if (message.Name == "SetForce")
            {
                var networkedObject = OdinWorld.Instance.GetNetworkObject((byte)message.GetValue("ObjectId"));
                if (networkedObject)
                {
                    var dir = (Vector3)message.GetValue("PushDir");
                    Debug.Log($"Adding Push {dir}");
                    networkedObject.GetComponent<Rigidbody>().AddForce(dir * 6.0f, ForceMode.Impulse);
                }
            }
        }
        
        public virtual void OnNameChanged(string oldValue, string newValue)
        {
            Debug.Log($"NAME CHANGED FROM {oldValue} TO {newValue}");
        }
    }
}

