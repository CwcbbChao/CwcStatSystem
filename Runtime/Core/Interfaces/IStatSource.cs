namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 属性提供源通用只读接口 (属性实例 StatContainer 与快照 StatSnapshot 均实现此接口)
    /// </summary>
    public interface IStatSource
    {
        /// <summary>
        /// 无上下文访问常态最终值 (标量单值或区间下限，0 纳秒、零额外计算)
        /// </summary>
        float GetValue(int statId);

        /// <summary>
        /// 传入瞬时 Tag 上下文进行纯函数求值 (无副作用，不污染底层数据)
        /// </summary>
        float GetValue(int statId, ulong contextTagMask);

        /// <summary>
        /// 无上下文访问常态区间最终值 (若为单值属性，Min == Max)
        /// </summary>
        StatRange GetRange(int statId);

        /// <summary>
        /// 传入瞬时 Tag 上下文访问区间最终值
        /// </summary>
        StatRange GetRange(int statId, ulong contextTagMask);

        /// <summary>
        /// 命中即时随机摇点 (在最终 [Min, Max] 区间中掷骰摇出一次点数)
        /// </summary>
        float RollValue(int statId);

        /// <summary>
        /// 传入瞬时 Tag 上下文并即时随机摇点
        /// </summary>
        float RollValue(int statId, ulong contextTagMask);

        /// <summary>
        /// 尝试获取属性槽位的烘焙数据结构
        /// </summary>
        bool TryGetSlot(int statId, out BakedStatSlot slot);
    }
}
