namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 属性修饰符乘区类型
    /// </summary>
    public enum ModifierType : byte
    {
        /// <summary>
        /// 基础加值 / 点伤 (直接累加到基础点数，吃后续百分比加成)
        /// </summary>
        FlatAdd = 0,

        /// <summary>
        /// 加算百分比 (Increased / Reduced, 累加后乘以 1 + sum)
        /// </summary>
        PercentAdd = 1,

        /// <summary>
        /// 独立增益乘区 (More / Less, 多个独立项按 1 + value 连乘)
        /// </summary>
        MoreMult = 2,

        /// <summary>
        /// 独立直接乘区 (Direct Multiplier, 多个独立项按 value 直接连乘，初始为 1.0)
        /// </summary>
        DirectMult = 3,

        /// <summary>
        /// 后置独立固定值 (不吃核心乘区百分比加成，直接在括号外部累加，防二次放大)
        /// </summary>
        PostFlatAdd = 4,

        /// <summary>
        /// 属性绝对值强制覆盖 (如定身移速设为0、固定属性锁定等，由属性定义决定小值/大值优先)
        /// </summary>
        Override = 5
    }
}
