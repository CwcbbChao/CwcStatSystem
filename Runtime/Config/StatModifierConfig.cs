using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 属性修饰符静态配置类 (享元模式设计，供装备词缀、Buff、技能等配置静态规则，动态修饰符共享其实例引用)
    /// </summary>
    [Serializable]
    public class StatModifierConfig : IEquatable<StatModifierConfig>
    {
        #region 常量与静态

        public const int NoSourceStat = -1;

        /// <summary>
        /// 全局只读的空配置回退实例，防止空引用
        /// </summary>
        public static readonly StatModifierConfig Empty = new StatModifierConfig(0, ModifierType.FlatAdd, 0, NoSourceStat);

        #endregion

        #region Inspector 字段

        [Tooltip("目标生效属性 ID")]
        [SerializeField] private int _targetStatId;

        [Tooltip("可选联动源属性 ID (-1 表示无联动常规词缀；>=0 表示该修饰符数值随源属性本体值按比例动态缩放)")]
        [SerializeField] private int _sourceStatId = NoSourceStat;

        [Tooltip("目标乘区类型")]
        [SerializeField] private ModifierType _modifyType = ModifierType.FlatAdd;

        [Tooltip("生效所需的上下文标签位掩码 (0 表示常态无条件)")]
        [SerializeField] private ulong _requiredTagMask = 0;

        #endregion

        #region 公共属性

        public int TargetStatId => _targetStatId;
        public int SourceStatId => _sourceStatId;
        public ModifierType ModifyType => _modifyType;
        public ulong RequiredTagMask => _requiredTagMask;
        public bool IsUnconditional => _requiredTagMask == 0;
        public bool IsScaled => _sourceStatId >= 0;

        #endregion

        #region 构造函数

        public StatModifierConfig()
        {
            _targetStatId = 0;
            _sourceStatId = NoSourceStat;
            _modifyType = ModifierType.FlatAdd;
            _requiredTagMask = 0;
        }

        public StatModifierConfig(
            int targetStatId,
            ModifierType modifyType,
            ulong requiredTagMask = 0,
            int sourceStatId = NoSourceStat)
        {
            _targetStatId = targetStatId;
            _sourceStatId = sourceStatId;
            _modifyType = modifyType;
            _requiredTagMask = requiredTagMask;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 传入单一标量数值或比例，生成修饰符 (Min == Max)
        /// </summary>
        public StatModifier ToModifier(float scalarValue)
        {
            return new StatModifier(this, new StatRange(scalarValue, scalarValue));
        }

        /// <summary>
        /// 传入范围上下限数值，生成范围修饰符
        /// </summary>
        public StatModifier ToModifier(float min, float max)
        {
            return new StatModifier(this, new StatRange(min, max));
        }

        /// <summary>
        /// 传入 StatRange 结构体，生成修饰符
        /// </summary>
        public StatModifier ToModifier(StatRange range)
        {
            return new StatModifier(this, range);
        }

        /// <summary>
        /// 传入参数列表，自动根据参数个数构造单值或范围修饰符
        /// </summary>
        public StatModifier ToModifier(IList<float> values)
        {
            if (values == null || values.Count == 0)
            {
                return new StatModifier(this, 0f);
            }

            if (values.Count == 1)
            {
                return ToModifier(values[0]);
            }

            return ToModifier(values[0], values[1]);
        }

        public bool Equals(StatModifierConfig other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _targetStatId == other._targetStatId &&
                   _sourceStatId == other._sourceStatId &&
                   _modifyType == other._modifyType &&
                   _requiredTagMask == other._requiredTagMask;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is StatModifierConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _targetStatId;
                hashCode = (hashCode * 397) ^ _sourceStatId;
                hashCode = (hashCode * 397) ^ (int)_modifyType;
                hashCode = (hashCode * 397) ^ _requiredTagMask.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(StatModifierConfig left, StatModifierConfig right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (ReferenceEquals(null, left) || ReferenceEquals(null, right)) return false;
            return left.Equals(right);
        }

        public static bool operator !=(StatModifierConfig left, StatModifierConfig right)
        {
            return !(left == right);
        }

        #endregion
    }
}
