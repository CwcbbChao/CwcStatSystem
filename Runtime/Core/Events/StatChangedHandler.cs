namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 属性数值发生实际变动时的回调委托
    /// </summary>
    /// <param name="oldValue">变化前的数值</param>
    /// <param name="newValue">变化后的最新数值</param>
    public delegate void StatChangedHandler(float oldValue, float newValue);
}
