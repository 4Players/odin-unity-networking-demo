using UnityEngine;

namespace Odin.OdinNetworking
{
    /// <summary>
    /// This script just tags a game object to be used to attach the AudioSource, i.e. the speaker of the player to the
    /// correct position. 
    /// </summary>
    public class OdinMouth : MonoBehaviour
    {
        private AudioSource _audioSource;
        
        /// <summary>
        /// The AudioSource that is attached to this script. It will be found in the game objects components.
        /// </summary>
        public AudioSource AudioSource
        {
            get
            {
                if (_audioSource == null)
                {
                    _audioSource = GetComponent<AudioSource>();
                }

                return _audioSource;
            }
        }
    }
}