namespace Odin.Audio
{
    public struct OdinAudioEffectData
    {
        public float Volume;
        public float CutoffFrequency;
        public float LowpassResonanceQ;


        public bool IsAudible =>
            Volume < 0.99f || Volume > 1.01f ||
            CutoffFrequency < 22000 ||
            LowpassResonanceQ < 0.99f ||
            LowpassResonanceQ > 1.01f;


        public static OdinAudioEffectData Default =>
            new OdinAudioEffectData
            {
                Volume = 1.0f,
                CutoffFrequency = 22000.0f,
                LowpassResonanceQ = 1.0f
            };
    }
}