using System;
using System.Collections.Generic;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 独立的修饰符纯数据集合 (装备、天赋、Buff 的词缀载体)
    /// </summary>
    public class ModifierCollection
    {
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        public IReadOnlyList<StatModifier> Modifiers => _modifiers;

        public event Action OnChanged;

        public void AddModifier(StatModifier modifier)
        {
            _modifiers.Add(modifier);
            OnChanged?.Invoke();
        }

        public void AddModifiers(IEnumerable<StatModifier> modifiers)
        {
            if (modifiers == null)
            {
                return;
            }

            _modifiers.AddRange(modifiers);
            OnChanged?.Invoke();
        }

        public bool RemoveModifier(StatModifier modifier)
        {
            if (_modifiers.Remove(modifier))
            {
                OnChanged?.Invoke();
                return true;
            }

            return false;
        }

        [Obsolete("Use AddModifier(StatModifier) with ModifierType.Override instead.")]
        public void AddOverride(StatOverride statOverride)
        {
            AddModifier(statOverride);
        }

        [Obsolete("Use RemoveModifier(StatModifier) with ModifierType.Override instead.")]
        public bool RemoveOverride(StatOverride statOverride)
        {
            return RemoveModifier(statOverride);
        }

        public void Clear()
        {
            if (_modifiers.Count == 0)
            {
                return;
            }

            _modifiers.Clear();
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 便捷方法：将自身集合生效给指定的属性容器
        /// </summary>
        public void ApplyTo(StatContainer container)
        {
            if (container != null)
            {
                container.AddSource(this);
            }
        }

        /// <summary>
        /// 便捷方法：从指定的属性容器中撤销自身集合
        /// </summary>
        public void RemoveFrom(StatContainer container)
        {
            if (container != null)
            {
                container.RemoveSource(this);
            }
        }
    }
}
