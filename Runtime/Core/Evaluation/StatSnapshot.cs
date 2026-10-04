using System;
using System.Collections.Generic;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 时态隔离轻量属性快照 (不可变、只读、完全脱离角色生命周期)
    /// </summary>
    public sealed class StatSnapshot : IStatSource, IDisposable
    {
        private static readonly Stack<BakedStatSlot[]> SlotArrayPool = new Stack<BakedStatSlot[]>();
        private static readonly object PoolLock = new object();

        private BakedStatSlot[] _slots;
        private readonly List<StatModifier> _conditionalModifiers = new List<StatModifier>();
        private ulong _castTagMask;
        private bool _isDisposed;

        public ulong CastTagMask => _castTagMask;
        public bool IsDisposed => _isDisposed;

        /// <summary>
        /// 内部初始化方法 (由 StatContainer 或内部工厂调用)
        /// </summary>
        internal void Initialize(
            BakedStatSlot[] sourceSlots,
            IReadOnlyList<StatModifier> sourceModifiers,
            ulong castTagMask)
        {
            _castTagMask = castTagMask;
            _isDisposed = false;

            var length = sourceSlots != null ? sourceSlots.Length : 0;
            _slots = RentSlots(length);
            if (sourceSlots != null && length > 0)
            {
                Array.Copy(sourceSlots, _slots, length);
            }

            _conditionalModifiers.Clear();
            if (sourceModifiers != null && sourceModifiers.Count > 0)
            {
                _conditionalModifiers.AddRange(sourceModifiers);
            }
        }

        public float GetValue(int statId)
        {
            return GetValue(statId, 0);
        }

        public float GetValue(int statId, ulong contextTagMask)
        {
            if (_isDisposed || _slots == null || statId < 0 || statId >= _slots.Length)
            {
                return 0f;
            }

            // 合并施法瞬间的固化 Tag 与本次命中的瞬时 Tag
            var effectiveTags = _castTagMask | contextTagMask;
            return StatKernel.Evaluate(
                _slots[statId],
                _conditionalModifiers,
                statId,
                effectiveTags);
        }

        public StatRange GetRange(int statId)
        {
            return GetRange(statId, 0);
        }

        public StatRange GetRange(int statId, ulong contextTagMask)
        {
            if (_isDisposed || _slots == null || statId < 0 || statId >= _slots.Length)
            {
                return default;
            }

            var effectiveTags = _castTagMask | contextTagMask;
            return StatKernel.EvaluateRange(
                _slots[statId],
                _conditionalModifiers,
                statId,
                effectiveTags);
        }

        public float RollValue(int statId)
        {
            return GetRange(statId).Roll();
        }

        public float RollValue(int statId, ulong contextTagMask)
        {
            return GetRange(statId, contextTagMask).Roll();
        }

        public bool TryGetSlot(int statId, out BakedStatSlot slot)
        {
            if (!_isDisposed && _slots != null && statId >= 0 && statId < _slots.Length)
            {
                slot = _slots[statId];
                return true;
            }

            slot = default;
            return false;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _conditionalModifiers.Clear();

            if (_slots != null)
            {
                ReturnSlots(_slots);
                _slots = null;
            }
        }

        private static BakedStatSlot[] RentSlots(int length)
        {
            lock (PoolLock)
            {
                if (SlotArrayPool.Count > 0)
                {
                    var array = SlotArrayPool.Pop();
                    if (array.Length >= length)
                    {
                        return array;
                    }
                }
            }

            return new BakedStatSlot[length];
        }

        private static void ReturnSlots(BakedStatSlot[] array)
        {
            lock (PoolLock)
            {
                SlotArrayPool.Push(array);
            }
        }
    }
}
