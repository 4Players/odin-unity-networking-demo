using Odin.Audio;
using UnityEngine;

namespace Odin.OdinNetworking
{
    /// <summary>
    /// This component handles the Audio Listener in the scene, i.e. the ears in the scene. As Unity does not have
    /// audio occlusion, ODIN comes with scripts implementing that feature. Audio occlusion happens if two players are
    /// standing a meter away, but a concrete wall is in between them. Unity adjusts volume only based on the distance
    /// and not based on the scene. As the setup is a bit complicated, this behaviour makes sure, that all required
    /// components are in place.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public class OdinEars : MonoBehaviour
    {
        /// <summary>
        /// The AudioListener that will listen to audio in the scene. Make sure to remove the AudioListener from the
        /// camera if this component is attached to a player (i.e. in third person controller games).
        /// </summary>
        [Tooltip("The Audio Listener used by this script. If not set it the local component will be used.")]
        [SerializeField] private AudioListener _audioListener;
        
        /// <summary>
        /// This script includes volume adjustments based on the direction. If a player talks in the opposite direction
        /// the volume is lower than if he talks directly towards you. Unity does not care about that and thus this
        /// script exists to handle these cases.
        /// </summary>
        [Tooltip("The Directional Audio Listener used by this script. If not set it the local component will be used.")]
        [SerializeField] private OdinDirectionalAudioListener _directionalAudioListener;
        
        /// <summary>
        /// As discussed in this class, this script handles audio occlusions, i.e. volume is dampened when a wall is
        /// between the players. 
        /// </summary>
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