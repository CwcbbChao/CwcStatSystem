using System;
using UnityEngine;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 单个属性的正交双通道烘焙槽位 (原生支持区间代数与范围伤害)
    /// </summary>
    public struct BakedStatSlot
    {
        // =========================================================================
        // 通道 A: 原生本体区 (只吃 SourceStatId < 0 的自身词缀)
        // 铁律: 对外输出协同给其他属性时，永远只读此处的 NativeValue！
        // =========================================================================
        public float NativeBaseMin;
        public float NativeBaseMax;
        public float NativeFlatMin;
        public float NativeFlatMax;
        public float NativePercentAdd;
        public float NativeMoreMult;
        public float NativeDirectMult;
        public float NativePostFlatMin;
        public float NativePostFlatMax;
        public float NativeValueMin;
        public float NativeValueMax;

        public float NativeValue => NativeValueMin;
        public StatRange NativeRange => new StatRange(NativeValueMin, NativeValueMax);

        // =========================================================================
        // 通道 B: 协同衍生区 (只接收 SourceStatId >= 0 的外部联动注入，绝不反向污染通道 A)
        // =========================================================================
        public float SynergyFlatMin;
        public float SynergyFlatMax;
        public float SynergyPercentAdd;
        public float SynergyMoreMult;
        public float SynergyDirectMult;
        public float SynergyPostFlatMin;
        public float SynergyPostFlatMax;

        // =========================================================================
        // 覆盖值状态
        // =========================================================================
        public bool HasOverride;
        public float OverrideMin;
        public float OverrideMax;
        public StatOverridePolicy OverridePolicy;

        public float OverrideValue => OverrideMin;
        public StatRange OverrideRange => new StatRange(OverrideMin, OverrideMax);

        // =========================================================================
        // 常态最终值缓存 (无上下文时直接读取，0 计算)
        // =========================================================================
        public float CachedRegularMin;
        public float CachedRegularMax;

        public float CachedRegularValue => CachedRegularMin;
        public StatRange CachedRegularRange => new StatRange(CachedRegularMin, CachedRegularMax);

        public void Reset(float defaultBaseValue, StatOverridePolicy overridePolicy = StatOverridePolicy.TakeMin)
        {
            Reset(defaultBaseValue, defaultBaseValue, overridePolicy);
        }

        public void Reset(float defaultBaseMin, float defaultBaseMax, StatOverridePolicy overridePolicy = StatOverridePolicy.TakeMin)
        {
            NativeBaseMin = defaultBaseMin;
            NativeBaseMax = defaultBaseMax;
            NativeFlatMin = 0f;
            NativeFlatMax = 0f;
            NativePercentAdd = 0f;
            NativeMoreMult = 1f;
            NativeDirectMult = 1f;
            NativePostFlatMin = 0f;
            NativePostFlatMax = 0f;
            NativeValueMin = defaultBaseMin;
            NativeValueMax = defaultBaseMax;

            SynergyFlatMin = 0f;
            SynergyFlatMax = 0f;
            SynergyPercentAdd = 0f;
            SynergyMoreMult = 1f;
            SynergyDirectMult = 1f;
            SynergyPostFlatMin = 0f;
            SynergyPostFlatMax = 0f;

            HasOverride = false;
            OverrideMin = 0f;
            OverrideMax = 0f;
            OverridePolicy = overridePolicy;

            CachedRegularMin = defaultBaseMin;
            CachedRegularMax = defaultBaseMax;
        }

        /// <summary>
        /// 第一趟扫描：计算纯净的 NativeValue (结合通道 A 内部核心乘区与后置固定值)
        /// </summary>
        public void CalculateNativeValue()
        {
            var mult = (1f + NativePercentAdd) * NativeMoreMult * NativeDirectMult;
            NativeValueMin = (NativeBaseMin + NativeFlatMin) * mult + NativePostFlatMin;
            NativeValueMax = (NativeBaseMax + NativeFlatMax) * mult + NativePostFlatMax;
        }

        /// <summary>
        /// 第二趟扫描后汇聚：结合通道 A 与通道 B，应用覆盖值，生成常态缓存 (计算纯粹无截断分支)
        /// </summary>
        public void FinalizeRegularValue()
        {
            if (HasOverride)
            {
                CachedRegularMin = OverrideMin;
                CachedRegularMax = OverrideMax;
                return;
            }

            var totalFlatMin = NativeBaseMin + NativeFlatMin + SynergyFlatMin;
            var totalFlatMax = NativeBaseMax + NativeFlatMax + SynergyFlatMax;
            var totalPercentAdd = NativePercentAdd + SynergyPercentAdd;
            var totalMoreMult = NativeMoreMult * SynergyMoreMult;
            var totalDirectMult = NativeDirectMult * SynergyDirectMult;
            var totalPostFlatMin = NativePostFlatMin + SynergyPostFlatMin;
            var totalPostFlatMax = NativePostFlatMax + SynergyPostFlatMax;

            var mult = (1f + totalPercentAdd) * totalMoreMult * totalDirectMult;
            CachedRegularMin = (totalFlatMin * mult) + totalPostFlatMin;
            CachedRegularMax = (totalFlatMax * mult) + totalPostFlatMax;
        }

        /// <summary>
        /// 应用单值覆盖仲裁
        /// </summary>
        public void ApplyOverride(float value)
        {
            ApplyOverride(value, value);
        }

        /// <summary>
        /// 应用范围覆盖仲裁
        /// </summary>
        public void ApplyOverride(float min, float max)
        {
            if (!HasOverride)
            {
                HasOverride = true;
                OverrideMin = min;
                OverrideMax = max;
                return;
            }

            if (OverridePolicy == StatOverridePolicy.TakeMin)
            {
                OverrideMin = Mathf.Min(OverrideMin, min);
                OverrideMax = Mathf.Min(OverrideMax, max);
            }
            else
            {
                OverrideMin = Mathf.Max(OverrideMin, min);
                OverrideMax = Mathf.Max(OverrideMax, max);
            }
        }
    }
}
