// Assets/Audio/SfxLibrary.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum SfxId
{
    None = 0,
    LetterClick,
    SlotRemove,
    UnlockButton,
    StartGame,
    CurrentAnswer,
    WrongAnswer,
    DoubleKill,
    TripleKill,
    QuadraKill,
    PentaKill,
    HexaKill,
    Flip,
    Tick,
    Reveal,
    MoneyTick,
    MoneySpawn,
    MoneyBuy,
    Woosh,
    ShutDown,
    // add more…
}

public enum SfxCategory
{
    Default = 0,
    UI      = 1,
    Gameplay= 2,
    Voice   = 3,
    Priority= 4,   // “big” SFX that should always duck BGM
}

[CreateAssetMenu(fileName="SfxLibrary", menuName="Audio/SFX Library")]
public sealed class SfxLibrary : ScriptableObject
{
    [Serializable]
    public struct Variant
    {
        public AudioClip clip;

        [Header("Mix")]
        [Range(0f, 2f)] public float volume;
        [Range(0.5f, 2f)] public float pitch;
        [Range(0f, 0.5f)] public float pitchJitter;
        [Min(0f)] public float weight;

        [Header("Category + Ducking")]
        public SfxCategory category;

        [Tooltip("If true, this variant requests BGM ducking (overrides category defaults).")]
        public bool overrideDuck;
        public bool duckBgm;       // only used if overrideDuck==true
        [Range(-30f, 0f)] public float duckDb;    // negative
        [Min(0f)] public float duckFadeOut;       // seconds
        [Min(0f)] public float duckFadeIn;        // seconds
    }

    [Serializable]
    public struct Bank
    {
        public SfxId id;
        public bool avoidImmediateRepeat;
        public List<Variant> variants;
    }

    [SerializeField] private List<Bank> banks = new();

    struct CompiledBank
    {
        public List<Variant> variants;
        public float totalWeight;
        public int lastIndex;
        public bool avoidRepeat;
    }

    Dictionary<SfxId, CompiledBank> _map;

    void OnEnable()
    {
        _map = new Dictionary<SfxId, CompiledBank>(banks.Count);
        foreach (var b in banks)
        {
            if (b.variants == null || b.variants.Count == 0) continue;

            float sum = 0f;
            foreach (var v in b.variants)
                if (v.clip && v.weight > 0f) sum += v.weight;

            if (sum <= 0f) continue;

            var cb = new CompiledBank
            {
                variants = new List<Variant>(b.variants.Count),
                totalWeight = sum,
                lastIndex = -1,
                avoidRepeat = b.avoidImmediateRepeat
            };

            foreach (var v in b.variants)
                if (v.clip && v.weight > 0f) cb.variants.Add(v);

            if (cb.variants.Count > 0)
                _map[b.id] = cb;
        }
    }

    public bool TryPick(SfxId id, out Variant v)
    {
        v = default;
        if (_map == null || !_map.TryGetValue(id, out var bank) || bank.variants.Count == 0)
            return false;

        if (bank.variants.Count == 1)
        {
            v = bank.variants[0];
            return true;
        }

        const int maxAttempts = 4;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float r = UnityEngine.Random.value * bank.totalWeight;
            float acc = 0f;
            for (int i = 0; i < bank.variants.Count; i++)
            {
                acc += bank.variants[i].weight;
                if (r <= acc)
                {
                    if (bank.avoidRepeat && i == bank.lastIndex)
                        break;
                    v = bank.variants[i];
                    bank.lastIndex = i;
                    _map[id] = bank;
                    return true;
                }
            }
        }

        int idx = UnityEngine.Random.Range(0, bank.variants.Count);
        v = bank.variants[idx];
        bank.lastIndex = idx;
        _map[id] = bank;
        return true;
    }

    public bool Has(SfxId id) => _map != null && _map.ContainsKey(id);
}