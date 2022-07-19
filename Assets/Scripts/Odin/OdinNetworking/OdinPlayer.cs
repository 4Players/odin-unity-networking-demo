using Odin.OdinNetworking.Messages;
using OdinNative.Odin.Room;
using UnityEngine;

namespace Odin.OdinNetworking
{
    /// <summary>
    /// This is the standard player class derived from <see cref="Odin.OdinNetworking.OdinNetworkIdentity"/>. This class
    /// provides a sync var with the name implements 3D spatial audio automatically.  
    /// </summary>
    /// <remarks>If you have a standard player avatar you can directly derive your player class from this class,
    /// otherwise derive your class from <see cref="Odin.OdinNetworking.OdinNetworkIdentity"/>.</remarks>
    public class OdinPlayer : OdinNetworkIdentity
    {
        /// <summary>
        /// Set an instance of <see cref="Odin.OdinNetworking.OdinEars"/> that implement audio occlusion and direction
        /// occlusion and a few other convenience functions. If not set in the inspector, an instance of <see cref="Odin.OdinNetworking.OdinEars"/>
        /// will be searched in the hierarchy. This represents the "ears" of your local player.
        /// </summary>
        [Header("Player Settings")]
        [Tooltip("Set an OdinEars instance. It's where the AudioListener and special AudioEffects are handled. If not set, it will seek in the hierarchy for it.")]
        public OdinEars odinEars;
        
        /// <summary>
        /// Represents the mouth of other player objects. It's where the "speaker" of their voice is positioned for 3D
        /// spatial audio. I.e. if you are standing in front of another player, audio comes out of his mouth. Create
        /// an instance of the <see cref="Odin.OdinNetworking.OdinMouth"/> component in the hierarchy and place it where
        /// the mouth of the player is located. This class will then do the rest to position media streams correctly
        /// at the players mouth. If not set, it will be searched in the hierarchy at start.
        /// </summary>
        [Tooltip("Set an OdinMouth instance. It's where the Audio output is positioned. Position that object near the mouth of the avatar.")]
        public OdinMouth odinMouth;

        /// <summary>
        /// The name of the player as a sync var. So each player in the network has a name. Use the OnNameChanged
        /// hook function to get notified once the name has been changed of a player in the network.
        /// </summary>
        [OdinSyncVar(hook = nameof(OnNameChanged))]
        [Tooltip("The name of the player that is used by default.")]
        public string Name;
        
        /// <summary>
        /// Sets up odinMouth and odinEars if they have not been set.
        /// </summary>
        /// <remarks>Call the base class if you override this function.</remarks>
        protected virtual void OnEnable()
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
        
        /// <summary>
        /// Returns the odinMouth game object. If not set this game object is returned. The script will position the
        /// AudioSource (i.e. the microphone output) to the returned game object. If you have more than one room connection
        /// (i.e. one for 3D audio and one for walkie talkie) you need to override the function to adjust behaviour.
        /// </summary>
        /// <param name="room">The room where the microphone has been enabled</param>
        /// <returns>The GameObject where the PlaybackComponent should be attached.</returns>
        public override GameObject GetPlaybackComponentContainer(Room room)
        {
            return odinMouth != null ? odinMouth.gameObject : gameObject;
        }

        /// <summary>
        /// Default implementation does nothing. Override this function to handle custom commands.
        /// </summary>
        /// <param name="message">The command message</param>
        public override void OnCommandReceived(OdinCommandMessage message)
        {
        }
        
        /// <summary>
        /// Hook function called whenever the name of the player has been changed.
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        public virtual void OnNameChanged(string oldValue, string newValue)
        {
            Debug.Log($"NAME CHANGED FROM {oldValue} TO {newValue}");
        }
    }
}

