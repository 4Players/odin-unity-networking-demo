using UnityEngine;

namespace Odin.OdinNetworking
{
    public class OdinMouth : MonoBehaviour
    {
        private AudioSource _audioSource;
        
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