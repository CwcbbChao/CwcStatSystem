namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 属性覆盖仲裁策略 (由属性定义唯一确定，消除修饰符间的真相不一致)
    /// </summary>
    public enum StatOverridePolicy : byte
    {
        /// <summary>
        /// 取更小值优先 (默认惩罚性/限制性规则，如定身移速为0压制减速50)
        /// </summary>
        TakeMin = 0,

        /// <summary>
        /// 取更大值优先 (保底性/强力增益规则)
        /// </summary>
        TakeMax = 1
    }
}
