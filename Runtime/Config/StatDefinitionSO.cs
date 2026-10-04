using UnityEngine;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 单个属性的核心定义资产 (原生极简：仅维护核心数值基底元数据，StatKey 自动对齐 SO 资产名，UI 展现由项目层按需自由扩展)
    /// </summary>
    [CreateAssetMenu(fileName = "NewStat", menuName = "CwcStatSystem/Stat Definition", order = 10)]
    public class StatDefinitionSO : ScriptableObject
    {
        #region Inspector 字段

        [Tooltip("无任何修饰符时的固有基底点数（增量属性默认为 0，百分比/倍率属性默认为 1）")]
        [SerializeField] private float _defaultBaseValue = 0f;

        [Tooltip("范围属性时的固有基底点数上限（仅在 IsRangeStat 为 true 时生效）")]
        [SerializeField] private float _defaultBaseValueMax = 0f;

        [Tooltip("此属性是否为范围属性 (如物理点伤、武器伤害区间等)")]
        [SerializeField] private bool _isRangeStat = false;

        [Tooltip("多项强制覆盖生效时的仲裁策略 (TakeMin: 取小值优先限制/惩罚; TakeMax: 取大值优先保底)")]
        [SerializeField] private StatOverridePolicy _overridePolicy = StatOverridePolicy.TakeMin;

        #endregion

        #region 公共属性

        /// <summary>
        /// 属性内部标识键名：完全对齐 ScriptableObject 资产文件名
        /// </summary>
        public string StatKey => name;

        public float DefaultBaseValue => _defaultBaseValue;
        public float DefaultBaseValueMax => IsRangeStat ? _defaultBaseValueMax : _defaultBaseValue;
        public virtual bool IsRangeStat => _isRangeStat;
        public StatOverridePolicy OverridePolicy => _overridePolicy;

        #endregion

        #region 公共方法

        public virtual StatDefinitionData ToData()
        {
            return new StatDefinitionData(_defaultBaseValue, DefaultBaseValueMax, IsRangeStat, _overridePolicy);
        }

        #endregion
    }
}
