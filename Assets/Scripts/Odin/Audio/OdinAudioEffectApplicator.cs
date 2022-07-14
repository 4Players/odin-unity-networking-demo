using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Odin.Audio
{
    /// <summary>
    ///     Script containing the behaviour for applying and removing both occlusion or direction effects on the connected
    ///     audio source. Multiple effects added during one frame will be accumulated according
    ///     to the <see cref="OdinAudioEffectDefinition" />'s <see cref="OdinAudioEffectDefinition.GetCombinedEffect" />
    ///     implementation.
    /// </summary>
    /// <remarks>
    ///     This script is added automatically by the directional or occlusion system, if not placed on an audio source.
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public class OdinAudioEffectApplicator : MonoBehaviour
    {
        /// <summary>
        /// If set to true, the audio occlusion system will remove the parent colliders of this occluded audio source
        /// from consideration as a occluding object. Should be set to false e.g. if you want any parent object to
        /// be an audio occlusion object.
        /// </summary>
        [SerializeField] private bool removeParentCollidersForOcclusion = true;
        
        private readonly List<OdinAudioEffectData> _effectList = new List<OdinAudioEffectData>();
        private AudioSource _audioSource;
        private AudioLowPassFilter _lowPassFilter;

        private OdinAudioEffectData _originalEffect;

        public bool RemoveParentCollidersForOcclusion => removeParentCollidersForOcclusion;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            Assert.IsNotNull(_audioSource);
            _audioSource.spatializePostEffects = true;

            _originalEffect.Volume = _audioSource.volume;

            _lowPassFilter = GetComponent<AudioLowPassFilter>();
            if (!_lowPassFilter) _lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
            _originalEffect.CutoffFrequency = _lowPassFilter.cutoffFrequency;
            _originalEffect.LowpassResonanceQ = _lowPassFilter.lowpassResonanceQ;
        }

        /// <summary>
        ///     Resets effects on the audio source to values applied at scene start.
        /// </summary>
        public void Reset()
        {
            ApplyInstant(_originalEffect);
        }

        private void Update()
        {
            var toApply = OdinAudioEffectData.Default;
            foreach (var effectData in _effectList)
                toApply = OdinAudioEffectDefinition.GetCombinedEffect(toApply, effectData);

            if (toApply.IsAudible)
            {
                _lowPassFilter.enabled = true;
                ApplyInstant(toApply);
            }
            else
            {
                _lowPassFilter.enabled = false;
                _audioSource.volume = _originalEffect.Volume;
            }

            _effectList.Clear();
        }

        /// <summary>
        ///     Applies the effect to the audio source. Multiple effects added during one frame will be occumulated according
        ///     to the <see cref="OdinAudioEffectDefinition" />'s <see cref="OdinAudioEffectDefinition.GetCombinedEffect" />
        ///     implementation.
        /// </summary>
        /// <param name="effectData"></param>
        public void Apply(OdinAudioEffectData effectData)
        {
            _effectList.Add(effectData);
        }

        private void ApplyInstant(OdinAudioEffectData effectData)
        {
            _lowPassFilter.cutoffFrequency = effectData.CutoffFrequency;
            _lowPassFilter.lowpassResonanceQ = effectData.LowpassResonanceQ;
            _audioSource.volume = _originalEffect.Volume * effectData.Volume;
        }
    }
}