using Odin.Audio;
using UnityEngine;

namespace Odin.OdinNetworking
{
    [RequireComponent(typeof(AudioListener))]
    public class OdinEars : MonoBehaviour
    {
        [SerializeField] private AudioListener _audioListener;
        [SerializeField] private OdinDirectionalAudioListener _directionalAudioListener;
        [SerializeField] private OdinOcclusionAudioListener _occlusionAudioListener;

        private void Awake()
        {
            if (!_audioListener) _audioListener = GetComponent<AudioListener>();
            if (!_directionalAudioListener) _directionalAudioListener = GetComponent<OdinDirectionalAudioListener>();
            if (!_occlusionAudioListener) _occlusionAudioListener = GetComponent<OdinOcclusionAudioListener>();
        }
    }
}