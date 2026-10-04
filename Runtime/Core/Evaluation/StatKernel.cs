using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 无状态属性计算核心 (纯函数，零堆分配，原生支持区间代数)
    /// </summary>
    public static class StatKernel
    {
        /// <summary>
        /// 统一属性求值方法 (返回 StatRange，兼容标量单值与范围伤害)
        /// </summary>
        public static StatRange EvaluateRange(
            in BakedStatSlot slot,
            IReadOnlyList<StatModifier> conditionalModifiers,
            int targetStatId,
            ulong contextTagMask)
        {
            // 1. 无上下文时，直接返回预烘焙好的常态缓存 (0 纳秒)
            if (contextTagMask == 0)
            {
                return slot.CachedRegularRange;
            }

            // 2. 提取双通道烘焙底数与覆盖状态
            var baseAndFlatMin = slot.NativeBaseMin + slot.NativeFlatMin + slot.SynergyFlatMin;
            var baseAndFlatMax = slot.NativeBaseMax + slot.NativeFlatMax + slot.SynergyFlatMax;
            var percentAdd = slot.NativePercentAdd + slot.SynergyPercentAdd;
            var moreMult = slot.NativeMoreMult * slot.SynergyMoreMult;
            var directMult = slot.NativeDirectMult * slot.SynergyDirectMult;
            var postFlatMin = slot.NativePostFlatMin + slot.SynergyPostFlatMin;
            var postFlatMax = slot.NativePostFlatMax + slot.SynergyPostFlatMax;

            var hasActiveOverride = slot.HasOverride;
            var activeOverrideMin = slot.OverrideMin;
            var activeOverrideMax = slot.OverrideMax;

            // 3. 叠加满足条件的条件修饰符 (包括条件覆盖值)
            if (conditionalModifiers != null)
            {
                var count = conditionalModifiers.Count;
                for (var i = 0; i < count; i++)
                {
                    var mod = conditionalModifiers[i];
                    if (mod.TargetStatId == targetStatId && mod.MatchesContext(contextTagMask))
                    {
                        switch (mod.ModifyType)
                        {
                            case ModifierType.FlatAdd:
                                baseAndFlatMin += mod.Value.Min;
                                baseAndFlatMax += mod.Value.Max;
                                break;
                            case ModifierType.PercentAdd:
                                percentAdd += mod.Value.Min;
                                break;
                            case ModifierType.MoreMult:
                                moreMult *= (1f + mod.Value.Min);
                                break;
                            case ModifierType.DirectMult:
                                directMult *= mod.Value.Min;
                                break;
                            case ModifierType.PostFlatAdd:
                                postFlatMin += mod.Value.Min;
                                postFlatMax += mod.Value.Max;
                                break;
                            case ModifierType.Override:
                                if (!hasActiveOverride)
                                {
                                    hasActiveOverride = true;
                                    activeOverrideMin = mod.Value.Min;
                                    activeOverrideMax = mod.Value.Max;
                                }
                                else
                                {
                                    if (slot.OverridePolicy == StatOverridePolicy.TakeMin)
                                    {
                                        activeOverrideMin = Mathf.Min(activeOverrideMin, mod.Value.Min);
                                        activeOverrideMax = Mathf.Min(activeOverrideMax, mod.Value.Max);
                                    }
                                    else
                                    {
                                        activeOverrideMin = Mathf.Max(activeOverrideMin, mod.Value.Min);
                                        activeOverrideMax = Mathf.Max(activeOverrideMax, mod.Value.Max);
                                    }
                                }
                                break;
                        }
                    }
                }
            }

            // 4. 若有生效的覆盖值，直接输出 (跳过加乘计算)
            if (hasActiveOverride)
            {
                return new StatRange(activeOverrideMin, activeOverrideMax);
            }

            // 5. 套用标准多层黄金乘区 (Min 与 Max 同步放大，后置固定值不受乘区影响)
            var mult = (1f + percentAdd) * moreMult * directMult;
            var calcMin = (baseAndFlatMin * mult) + postFlatMin;
            var calcMax = (baseAndFlatMax * mult) + postFlatMax;
            return new StatRange(calcMin, calcMax);
        }

        /// <summary>
        /// 标量快捷求值方法 (向后兼容，返回 Min / 单值)
        /// </summary>
        public static float Evaluate(
            in BakedStatSlot slot,
            IReadOnlyList<StatModifier> conditionalModifiers,
            int targetStatId,
            ulong contextTagMask)
        {
            return EvaluateRange(slot, conditionalModifiers, targetStatId, contextTagMask).Min;
        }
    }
}
