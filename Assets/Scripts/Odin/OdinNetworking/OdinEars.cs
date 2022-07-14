using System;
using ODIN_Sample.Scripts.Runtime.Audio;
using UnityEngine;

namespace Odin.OdinNetworking
{
    [RequireComponent(typeof(AudioListener))]
    public class OdinEars : MonoBehaviour
    {
        [SerializeField] private AudioListener _audioListener;
        [SerializeField] private DirectionalAudioListener _directionalAudioListener;
        [SerializeField] private OcclusionAudioListener _occlusionAudioListener;

        private void Awake()
        {
            if (!_audioListener) _audioListener = GetComponent<AudioListener>();
            if (!_directionalAudioListener) _directionalAudioListener = GetComponent<DirectionalAudioListener>();
            if (!_occlusionAudioListener) _occlusionAudioListener = GetComponent<OcclusionAudioListener>();
        }
    }
}