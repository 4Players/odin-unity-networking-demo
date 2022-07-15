using Odin.Audio;
using UnityEngine;

namespace Odin.OdinNetworking
{
    [RequireComponent(typeof(AudioListener))]
    public class OdinEars : MonoBehaviour
    {
        [Tooltip("The Audio Listener used by this script. If not set it the local component will be used.")]
        [SerializeField] private AudioListener _audioListener;
        
        [Tooltip("The Directional Audio Listener used by this script. If not set it the local component will be used.")]
        [SerializeField] private OdinDirectionalAudioListener _directionalAudioListener;
        
        [Tooltip("The Occlusion Audio Listener used by this script. If not set it the local component will be used.")]
        [SerializeField] private OdinOcclusionAudioListener _occlusionAudioListener;

        private void Awake()
        {
            if (!_audioListener) _audioListener = GetComponent<AudioListener>();
            if (!_directionalAudioListener) _directionalAudioListener = GetComponent<OdinDirectionalAudioListener>();
            if (!_occlusionAudioListener) _occlusionAudioListener = GetComponent<OdinOcclusionAudioListener>();
        }
    }
}