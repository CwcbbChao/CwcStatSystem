namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 属性基础元数据快照结构体 (用于解耦 SO 引用并传递基础约束给评估器)
    /// </summary>
    public readonly struct StatDefinitionData
    {
        public readonly float DefaultBaseValue;
        public readonly float DefaultBaseValueMax;
        public readonly bool IsRangeStat;
        public readonly StatOverridePolicy OverridePolicy;

        public StatDefinitionData(
            float defaultBaseValue,
            StatOverridePolicy overridePolicy = StatOverridePolicy.TakeMin)
        {
            DefaultBaseValue = defaultBaseValue;
            DefaultBaseValueMax = defaultBaseValue;
            IsRangeStat = false;
            OverridePolicy = overridePolicy;
        }

        public StatDefinitionData(
            float defaultBaseValueMin,
            float defaultBaseValueMax,
            StatOverridePolicy overridePolicy = StatOverridePolicy.TakeMin)
        {
            DefaultBaseValue = defaultBaseValueMin;
            DefaultBaseValueMax = defaultBaseValueMax;
            IsRangeStat = true;
            OverridePolicy = overridePolicy;
        }

        public StatDefinitionData(
            float defaultBaseValueMin,
            float defaultBaseValueMax,
            bool isRangeStat,
            StatOverridePolicy overridePolicy = StatOverridePolicy.TakeMin)
        {
            DefaultBaseValue = defaultBaseValueMin;
            DefaultBaseValueMax = defaultBaseValueMax;
            IsRangeStat = isRangeStat;
            OverridePolicy = overridePolicy;
        }

        public static StatDefinitionData Default => new StatDefinitionData(0f, StatOverridePolicy.TakeMin);
    }
}
