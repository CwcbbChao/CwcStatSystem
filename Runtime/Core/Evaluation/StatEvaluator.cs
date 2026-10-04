using System;
using System.Collections.Generic;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 双通道正交两趟极速线性扫描评估器 (原生支持区间代数与范围伤害)
    /// </summary>
    public static class StatEvaluator
    {
        /// <summary>
        /// 执行两趟线性扫描，烘焙所有槽位的常态数值，并收集条件修饰符
        /// </summary>
        public static void EvaluateAll(
            BakedStatSlot[] slots,
            Func<int, StatDefinitionData> definitionGetter,
            IReadOnlyList<ModifierCollection> sources,
            List<StatModifier> outConditionalModifiers)
        {
            if (slots == null)
            {
                return;
            }

            var numStats = slots.Length;

            // 0. 重置所有槽位与条件容器
            outConditionalModifiers?.Clear();

            for (var i = 0; i < numStats; i++)
            {
                var def = definitionGetter != null ? definitionGetter(i) : StatDefinitionData.Default;
                slots[i].Reset(def.DefaultBaseValue, def.DefaultBaseValueMax, def.OverridePolicy);
            }

            // =========================================================================
            // 第一趟 (Pass 1: 原生扫描 - 处理 SourceStatId < 0 的自身词缀，折叠进通道 A)
            // =========================================================================
            if (sources != null)
            {
                var sourceCount = sources.Count;
                for (var s = 0; s < sourceCount; s++)
                {
                    var source = sources[s];
                    if (source == null)
                    {
                        continue;
                    }

                    var modifiers = source.Modifiers;
                    var modCount = modifiers.Count;
                    for (var m = 0; m < modCount; m++)
                    {
                        var mod = modifiers[m];
                        if (mod.IsScaled)
                        {
                            // 联动修饰符留到第二趟扫描统一处理
                            continue;
                        }

                        var statId = mod.TargetStatId;
                        if (statId < 0 || statId >= numStats)
                        {
                            continue;
                        }

                        if (mod.IsUnconditional)
                        {
                            // 常态自身修饰符直接折叠进通道 A
                            switch (mod.ModifyType)
                            {
                                case ModifierType.FlatAdd:
                                    slots[statId].NativeFlatMin += mod.Value.Min;
                                    slots[statId].NativeFlatMax += mod.Value.Max;
                                    break;
                                case ModifierType.PercentAdd:
                                    slots[statId].NativePercentAdd += mod.Value.Min;
                                    break;
                                case ModifierType.MoreMult:
                                    slots[statId].NativeMoreMult *= (1f + mod.Value.Min);
                                    break;
                                case ModifierType.DirectMult:
                                    slots[statId].NativeDirectMult *= mod.Value.Min;
                                    break;
                                case ModifierType.PostFlatAdd:
                                    slots[statId].NativePostFlatMin += mod.Value.Min;
                                    slots[statId].NativePostFlatMax += mod.Value.Max;
                                    break;
                                case ModifierType.Override:
                                    slots[statId].ApplyOverride(mod.Value.Min, mod.Value.Max);
                                    break;
                            }
                        }
                        else
                        {
                            // 条件修饰符存入条件池，留待战斗命中时瞬时匹配
                            outConditionalModifiers?.Add(mod);
                        }
                    }
                }
            }

            // 汇聚所有属性的通道 A 原生本体值 (NativeValue)
            for (var i = 0; i < numStats; i++)
            {
                slots[i].CalculateNativeValue();
            }

            // =========================================================================
            // 第二趟 (Pass 2: 属性联动扫描 - 纯读取源属性通道 A 的 NativeValue，注入目标通道 B)
            // =========================================================================
            if (sources != null)
            {
                var sourceCount = sources.Count;
                for (var s = 0; s < sourceCount; s++)
                {
                    var source = sources[s];
                    if (source == null)
                    {
                        continue;
                    }

                    var modifiers = source.Modifiers;
                    var modCount = modifiers.Count;
                    for (var m = 0; m < modCount; m++)
                    {
                        var mod = modifiers[m];
                        if (!mod.IsScaled)
                        {
                            // 非联动修饰符已在第一趟处理完毕
                            continue;
                        }

                        var srcId = mod.SourceStatId;
                        var tgtId = mod.TargetStatId;
                        if (srcId < 0 || srcId >= numStats || tgtId < 0 || tgtId >= numStats)
                        {
                            continue;
                        }

                        if (!mod.IsUnconditional)
                        {
                            outConditionalModifiers?.Add(mod);
                            continue;
                        }

                        // 铁律：永远只读取源属性第一趟烘焙出的 NativeValue！
                        var sourceNative = slots[srcId].NativeValue;

                        switch (mod.ModifyType)
                        {
                            case ModifierType.FlatAdd:
                                slots[tgtId].SynergyFlatMin += sourceNative * mod.Value.Min;
                                slots[tgtId].SynergyFlatMax += sourceNative * mod.Value.Max;
                                break;
                            case ModifierType.PercentAdd:
                                slots[tgtId].SynergyPercentAdd += sourceNative * mod.Value.Min;
                                break;
                            case ModifierType.MoreMult:
                                slots[tgtId].SynergyMoreMult *= (1f + sourceNative * mod.Value.Min);
                                break;
                            case ModifierType.DirectMult:
                                slots[tgtId].SynergyDirectMult *= (sourceNative * mod.Value.Min);
                                break;
                            case ModifierType.PostFlatAdd:
                                slots[tgtId].SynergyPostFlatMin += sourceNative * mod.Value.Min;
                                slots[tgtId].SynergyPostFlatMax += sourceNative * mod.Value.Max;
                                break;
                            case ModifierType.Override:
                                var overrideVal = sourceNative * mod.Value.Min;
                                slots[tgtId].ApplyOverride(overrideVal, overrideVal);
                                break;
                        }
                    }
                }
            }

            // =========================================================================
            // 汇聚阶段 (Finalize: 结合通道 A 与 B，生成常态缓存)
            // =========================================================================
            for (var i = 0; i < numStats; i++)
            {
                slots[i].FinalizeRegularValue();
            }
        }
    }
}
