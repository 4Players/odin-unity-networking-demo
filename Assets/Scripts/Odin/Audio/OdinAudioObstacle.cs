using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

namespace Odin.Audio
{
    /// <summary>
    ///     Data component, containing settings for overwriting the default occlusion behavior of objects.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class OdinAudioObstacle : MonoBehaviour
    {
        /// <summary>
        ///     Reference to a scriptable object, containing the Effect Settings Data.
        /// </summary>
        [FormerlySerializedAs("settings")] public OdinAudioEffectDefinition effect;

        private void Awake()
        {
            Assert.IsNotNull(effect);
        }
    }
}