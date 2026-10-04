using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 紧凑的区间数值结构体 (区间代数模型，标量即 Min == Max 的单点区间)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public readonly struct StatRange : IEquatable<StatRange>
    {
        public readonly float Min;
        public readonly float Max;

        public bool IsRange => Min != Max;
        public float Average => (Min + Max) * 0.5f;

        public StatRange(float scalar)
        {
            Min = scalar;
            Max = scalar;
        }

        public StatRange(float min, float max)
        {
            if (min <= max)
            {
                Min = min;
                Max = max;
            }
            else
            {
                // 容错处理：确保 Min 始终不大于 Max
                Min = max;
                Max = min;
            }
        }

        [ThreadStatic] private static System.Random _sharedRng;
        private static System.Random SharedRng => _sharedRng ?? (_sharedRng = new System.Random());

        /// <summary>
        /// 战斗命中摇点 (在 [Min, Max] 均匀随机出一个点数，线程安全且无 Unity 引擎 ECall 依赖)
        /// </summary>
        public float Roll()
        {
            if (Min == Max)
            {
                return Min;
            }

            return Roll(SharedRng);
        }

        /// <summary>
        /// 使用指定随机数生成器摇点 (便于无头服务器或固定种子回放)
        /// </summary>
        public float Roll(System.Random rng)
        {
            if (Min == Max)
            {
                return Min;
            }

            var generator = rng ?? SharedRng;
            return Min + (float)generator.NextDouble() * (Max - Min);
        }

        public StatRange Clamp(float minLimit, float maxLimit)
        {
            return new StatRange(
                Mathf.Clamp(Min, minLimit, maxLimit),
                Mathf.Clamp(Max, minLimit, maxLimit));
        }

        public bool Equals(StatRange other)
        {
            return Min.Equals(other.Min) && Max.Equals(other.Max);
        }

        public override bool Equals(object obj)
        {
            return obj is StatRange other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Min.GetHashCode() * 397) ^ Max.GetHashCode();
            }
        }

        public override string ToString()
        {
            return IsRange ? $"[{Min:0.##} ~ {Max:0.##}]" : Min.ToString("0.##");
        }

        public static StatRange operator +(StatRange a, StatRange b)
        {
            return new StatRange(a.Min + b.Min, a.Max + b.Max);
        }

        public static StatRange operator -(StatRange a, StatRange b)
        {
            return new StatRange(a.Min - b.Min, a.Max - b.Max);
        }

        public static StatRange operator *(StatRange a, float scalar)
        {
            return new StatRange(a.Min * scalar, a.Max * scalar);
        }

        public static bool operator ==(StatRange left, StatRange right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StatRange left, StatRange right)
        {
            return !left.Equals(right);
        }

        public static implicit operator StatRange(float scalar)
        {
            return new StatRange(scalar);
        }

        public static implicit operator float(StatRange range)
        {
            return range.Min;
        }
    }
}
