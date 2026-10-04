using System;
using System.Runtime.InteropServices;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 统一紧凑的不可变属性修饰符结构体 (享元模式：8字节配置引用指针 + 8字节数值范围，内存仅 16 字节极速传参)
    /// </summary>
    public readonly struct StatModifier : IEquatable<StatModifier>
    {
        public readonly StatModifierConfig Config;
        public readonly StatRange Value;

        public int TargetStatId => Config.TargetStatId;
        public int SourceStatId => Config.SourceStatId;
        public ModifierType ModifyType => Config.ModifyType;
        public ulong RequiredTagMask => Config.RequiredTagMask;
        public bool IsUnconditional => Config.IsUnconditional;
        public bool IsScaled => Config.IsScaled;
        public float MinValue => Value.Min;
        public float MaxValue => Value.Max;
        public float ScalarValue => Value.Min;

        public StatModifier(StatModifierConfig config, StatRange value)
        {
            Config = config ?? StatModifierConfig.Empty;
            Value = value;
        }

        public StatModifier(StatModifierConfig config, float scalarValue)
        {
            Config = config ?? StatModifierConfig.Empty;
            Value = new StatRange(scalarValue, scalarValue);
        }

        public StatModifier(StatModifierConfig config, float min, float max)
        {
            Config = config ?? StatModifierConfig.Empty;
            Value = new StatRange(min, max);
        }

        public StatModifier(int targetStatId, ModifierType modifyType, float scalarValue, ulong requiredTagMask = 0, int sourceStatId = StatModifierConfig.NoSourceStat)
        {
            Config = new StatModifierConfig(targetStatId, modifyType, requiredTagMask, sourceStatId);
            Value = new StatRange(scalarValue, scalarValue);
        }

        public StatModifier(int targetStatId, ModifierType modifyType, float min, float max, ulong requiredTagMask = 0, int sourceStatId = StatModifierConfig.NoSourceStat)
        {
            Config = new StatModifierConfig(targetStatId, modifyType, requiredTagMask, sourceStatId);
            Value = new StatRange(min, max);
        }

        /// <summary>
        /// 便捷构造：创建跨属性联动修饰符 (以指定的比率按目标乘区从源属性中派生增益)
        /// </summary>
        public StatModifier(int targetStatId, int sourceStatId, ModifierType modifyType, float ratio, ulong requiredTagMask = 0)
        {
            Config = new StatModifierConfig(targetStatId, modifyType, requiredTagMask, sourceStatId);
            Value = new StatRange(ratio, ratio);
        }

        public bool MatchesContext(ulong contextTagMask)
        {
            if (RequiredTagMask == 0)
            {
                return true;
            }

            return (contextTagMask & RequiredTagMask) == RequiredTagMask;
        }

        public bool Equals(StatModifier other)
        {
            return Equals(Config, other.Config) && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is StatModifier other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Config != null ? Config.GetHashCode() : 0) * 397) ^ Value.GetHashCode();
            }
        }

        public static bool operator ==(StatModifier left, StatModifier right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StatModifier left, StatModifier right)
        {
            return !left.Equals(right);
        }
    }
}
