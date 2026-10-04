using System;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 已废弃：覆盖值现已统一收敛至 ModifierType.Override。请直接使用 StatModifier。
    /// </summary>
    [Obsolete("StatOverride is deprecated. Use new StatModifier(targetStatId, ModifierType.Override, value, requiredTagMask) instead.")]
    public readonly struct StatOverride
    {
        public readonly int TargetStatId;
        public readonly float Value;
        public readonly ulong RequiredTagMask;

        public StatOverride(int targetStatId, float value, int priority = 0, StatOverridePolicy policy = StatOverridePolicy.TakeMin, ulong requiredTagMask = 0)
        {
            TargetStatId = targetStatId;
            Value = value;
            RequiredTagMask = requiredTagMask;
        }

        public static implicit operator StatModifier(StatOverride statOverride)
        {
            return new StatModifier(statOverride.TargetStatId, ModifierType.Override, statOverride.Value, statOverride.RequiredTagMask);
        }
    }
}
