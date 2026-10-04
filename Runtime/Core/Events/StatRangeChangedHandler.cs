namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 区间属性数值发生实际变动时的回调委托
    /// </summary>
    /// <param name="oldValue">变化前的区间数值</param>
    /// <param name="newValue">变化后的最新区间数值</param>
    public delegate void StatRangeChangedHandler(StatRange oldValue, StatRange newValue);
}
