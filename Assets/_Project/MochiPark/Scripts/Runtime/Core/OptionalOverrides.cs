using System;
using System.Collections.Generic;
using UnityEngine;

namespace MochiPark.Runtime.Core
{
    [Serializable]
    public struct OptionalInt
    {
        [SerializeField] private bool hasValue;
        [SerializeField] private int value;

        public bool HasValue => hasValue;
        public int Value => value;

        public int Resolve(int fallback)
        {
            return hasValue ? value : fallback;
        }
    }

    [Serializable]
    public struct OptionalFloat
    {
        [SerializeField] private bool hasValue;
        [SerializeField] private float value;

        public bool HasValue => hasValue;
        public float Value => value;

        public float Resolve(float fallback)
        {
            return hasValue ? value : fallback;
        }
    }

    [Serializable]
    public struct OptionalBool
    {
        [SerializeField] private bool hasValue;
        [SerializeField] private bool value;

        public bool HasValue => hasValue;
        public bool Value => value;

        public bool Resolve(bool fallback)
        {
            return hasValue ? value : fallback;
        }
    }

    [Serializable]
    public sealed class OptionalFloatList
    {
        [SerializeField] private bool hasValue;
        [SerializeField] private List<float> values = new List<float>();

        public bool HasValue => hasValue;
        public IReadOnlyList<float> Values => values;

        public List<float> Resolve(IReadOnlyList<float> fallback)
        {
            if (!hasValue || values == null || values.Count == 0)
            {
                return fallback == null ? new List<float>() : new List<float>(fallback);
            }

            return new List<float>(values);
        }
    }
}
