using System;
using System.Collections.Generic;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 角色实体属性主容器 (负责插槽拉取、内置惰性脏标记、按需烘焙与快照导出)
    /// </summary>
    public class StatContainer : IStatSource, IDisposable
    {
        public const float ChangeEpsilon = 1e-5f;

        private BakedStatSlot[] _slots;
        private StatRange[] _previousRanges;
        private StatChangedHandler[] _statChangedHandlers;
        private StatRangeChangedHandler[] _rangeChangedHandlers;
        private readonly ModifierCollection _internalModifiers = new ModifierCollection();
        private readonly List<ModifierCollection> _externalSources = new List<ModifierCollection>();
        private readonly List<ModifierCollection> _allSources = new List<ModifierCollection>();
        private readonly List<StatModifier> _conditionalModifiers = new List<StatModifier>();

        private Func<int, StatDefinitionData> _definitionGetter;
        private Action _onRecalculated;
        private bool _isDirty = true;
        private bool _autoFlush = true;
        private bool _isEvaluating;
        private int _batchDepth;
        private int _activeSubscriptionCount;
        internal bool _isInDriverQueue;

        public event Action OnRecalculated
        {
            add
            {
                _onRecalculated += value;
                OnListenersChanged();
            }
            remove
            {
                _onRecalculated -= value;
                OnListenersChanged();
            }
        }

        public int StatCount => _slots != null ? _slots.Length : 0;
        public bool IsDirty => _isDirty;
        public bool AutoFlush
        {
            get => _autoFlush;
            set => _autoFlush = value;
        }
        public bool HasActiveListeners => _activeSubscriptionCount > 0 || _onRecalculated != null;
        public bool IsInDriverQueue => _isInDriverQueue;
        public IReadOnlyList<ModifierCollection> Sources => _externalSources;
        public IReadOnlyList<StatModifier> Modifiers => _internalModifiers.Modifiers;

        public StatContainer(int numStats, Func<int, StatDefinitionData> definitionGetter = null)
        {
            _slots = new BakedStatSlot[numStats];
            _previousRanges = new StatRange[numStats];
            _statChangedHandlers = new StatChangedHandler[numStats];
            _rangeChangedHandlers = new StatRangeChangedHandler[numStats];
            _definitionGetter = definitionGetter;
            InitializePreviousRanges();
            _internalModifiers.OnChanged += MarkDirty;
            _allSources.Add(_internalModifiers);
            _isDirty = true;
        }

        public void SetStatCount(int numStats)
        {
            if (_slots == null || _slots.Length != numStats)
            {
                _slots = new BakedStatSlot[numStats];
                Array.Resize(ref _previousRanges, numStats);
                Array.Resize(ref _statChangedHandlers, numStats);
                Array.Resize(ref _rangeChangedHandlers, numStats);
                InitializePreviousRanges();
                MarkDirty();
            }
        }

        public void SetDefinitionGetter(Func<int, StatDefinitionData> definitionGetter)
        {
            _definitionGetter = definitionGetter;
            InitializePreviousRanges();
            MarkDirty();
        }

        /// <summary>
        /// 向容器内部自带的修饰符集合添加单个修饰符
        /// </summary>
        public void AddModifier(StatModifier modifier)
        {
            _internalModifiers.AddModifier(modifier);
        }

        /// <summary>
        /// 向容器内部自带的修饰符集合批量添加修饰符
        /// </summary>
        public void AddModifiers(IEnumerable<StatModifier> modifiers)
        {
            _internalModifiers.AddModifiers(modifiers);
        }

        /// <summary>
        /// 从容器内部自带的修饰符集合移除单个修饰符
        /// </summary>
        public bool RemoveModifier(StatModifier modifier)
        {
            return _internalModifiers.RemoveModifier(modifier);
        }

        /// <summary>
        /// 清空容器内部自带的修饰符集合
        /// </summary>
        public void ClearModifiers()
        {
            _internalModifiers.Clear();
        }

        /// <summary>
        /// 将自身的内部修饰符集合作为一个整体应用给目标属性容器生效 (如光环、从属继承、属性共享)
        /// </summary>
        public void ApplyModifiersTo(StatContainer target)
        {
            if (target == null || target == this)
            {
                return;
            }

            target.AddSource(_internalModifiers);
        }

        /// <summary>
        /// 从目标属性容器中撤销自身内部修饰符集合的生效
        /// </summary>
        public void RemoveModifiersFrom(StatContainer target)
        {
            if (target == null || target == this)
            {
                return;
            }

            target.RemoveSource(_internalModifiers);
        }

        public void AddSource(ModifierCollection source)
        {
            if (source == null || source == _internalModifiers)
            {
                return;
            }

            if (!_externalSources.Contains(source))
            {
                _externalSources.Add(source);
                _allSources.Add(source);
                source.OnChanged += MarkDirty;
                MarkDirty();
            }
        }

        public void RemoveSource(ModifierCollection source)
        {
            if (source == null || source == _internalModifiers)
            {
                return;
            }

            if (_externalSources.Remove(source))
            {
                _allSources.Remove(source);
                source.OnChanged -= MarkDirty;
                MarkDirty();
            }
        }

        public void ClearSources()
        {
            if (_externalSources.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _externalSources.Count; i++)
            {
                if (_externalSources[i] != null)
                {
                    _externalSources[i].OnChanged -= MarkDirty;
                }
            }

            _externalSources.Clear();
            _allSources.Clear();
            _allSources.Add(_internalModifiers);
            MarkDirty();
        }

        public void MarkDirty()
        {
            _isDirty = true;
            // 关键优化：只有开启自动刷新、不在批量更新中、且存在活跃观察者时才向集中管理器报备
            if (_autoFlush && _batchDepth == 0 && HasActiveListeners)
            {
                StatUpdateDriver.RegisterDirty(this);
            }
        }

        /// <summary>
        /// 开启批量操作作用域，防止循环穿脱装备时发生多次无效重算 (返回结构体避免装箱 GC)
        /// </summary>
        public BatchScope BeginBatchUpdate()
        {
            _batchDepth++;
            return new BatchScope(this);
        }

        /// <summary>
        /// 确保属性已按需刷新完毕 (并在数值跃迁时定向派发细粒度属性变动事件)
        /// </summary>
        public void EnsureClean()
        {
            if (!_isDirty || _batchDepth > 0 || _isEvaluating)
            {
                return;
            }

            _isEvaluating = true;
            _isDirty = false;

            try
            {
                StatEvaluator.EvaluateAll(
                    _slots,
                    _definitionGetter,
                    _allSources,
                    _conditionalModifiers);

                if (_slots != null && _previousRanges != null)
                {
                    var count = _slots.Length;
                    for (var i = 0; i < count; i++)
                    {
                        var newRange = _slots[i].CachedRegularRange;
                        var oldRange = _previousRanges[i];

                        var minChanged = Math.Abs(newRange.Min - oldRange.Min) > ChangeEpsilon;
                        var maxChanged = Math.Abs(newRange.Max - oldRange.Max) > ChangeEpsilon;

                        if (minChanged || maxChanged)
                        {
                            _previousRanges[i] = newRange;

                            // 派发区间变动事件 (只要 Min 或 Max 任意一端发生变动即派发)
                            var rangeHandler = _rangeChangedHandlers?[i];
                            if (rangeHandler != null)
                            {
                                InvokeSafe(rangeHandler, oldRange, newRange);
                            }

                            // 派发标量变动事件 (仅在 Min 发生实质变动时派发，防骚扰且完美向下兼容)
                            if (minChanged)
                            {
                                var statHandler = _statChangedHandlers?[i];
                                if (statHandler != null)
                                {
                                    InvokeSafe(statHandler, oldRange.Min, newRange.Min);
                                }
                            }
                        }
                    }
                }

                _onRecalculated?.Invoke();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[StatContainer] EnsureClean 执行重算或派发异常: {ex}");
            }
            finally
            {
                _isEvaluating = false;
            }
        }

        public float GetValue(int statId)
        {
            EnsureClean();
            if (_slots == null || statId < 0 || statId >= _slots.Length)
            {
                return 0f;
            }

            // 无 Tag 时直接读取烘焙好的缓存字段 (0 纳秒)
            return _slots[statId].CachedRegularValue;
        }

        public float GetValue(int statId, ulong contextTagMask)
        {
            EnsureClean();
            if (_slots == null || statId < 0 || statId >= _slots.Length)
            {
                return 0f;
            }

            return StatKernel.Evaluate(
                _slots[statId],
                _conditionalModifiers,
                statId,
                contextTagMask);
        }

        public StatRange GetRange(int statId)
        {
            EnsureClean();
            if (_slots == null || statId < 0 || statId >= _slots.Length)
            {
                return default;
            }

            return _slots[statId].CachedRegularRange;
        }

        public StatRange GetRange(int statId, ulong contextTagMask)
        {
            EnsureClean();
            if (_slots == null || statId < 0 || statId >= _slots.Length)
            {
                return default;
            }

            return StatKernel.EvaluateRange(
                _slots[statId],
                _conditionalModifiers,
                statId,
                contextTagMask);
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
            EnsureClean();
            if (_slots != null && statId >= 0 && statId < _slots.Length)
            {
                slot = _slots[statId];
                return true;
            }

            slot = default;
            return false;
        }

        /// <summary>
        /// 导出当前时刻的时态隔离快照 (供技能、飞行道具或异步延迟结算持有)
        /// </summary>
        public StatSnapshot CreateSnapshot(ulong castTagMask = 0)
        {
            EnsureClean();
            var snapshot = new StatSnapshot();
            snapshot.Initialize(_slots, _conditionalModifiers, castTagMask);
            return snapshot;
        }

        /// <summary>
        /// 订阅指定属性数值发生变化时的通知 (标量数值)
        /// </summary>
        /// <param name="statId">属性 ID</param>
        /// <param name="handler">数值变化回调 (旧值, 新值)</param>
        /// <param name="triggerImmediately">是否在订阅时以当前最新数值立即回调一次</param>
        /// <returns>用于注销订阅的 IDisposable 凭据</returns>
        public IDisposable Subscribe(int statId, StatChangedHandler handler, bool triggerImmediately = false)
        {
            if (statId < 0 || statId >= StatCount || handler == null)
            {
                return null;
            }

            if (triggerImmediately)
            {
                var currentVal = GetValue(statId);
                handler.Invoke(currentVal, currentVal);
            }

            _statChangedHandlers[statId] += handler;
            _activeSubscriptionCount++;
            OnListenersChanged();

            return new ScalarSubscriptionScope(this, statId, handler);
        }

        /// <summary>
        /// 取消对指定属性标量变化的订阅
        /// </summary>
        public void Unsubscribe(int statId, StatChangedHandler handler)
        {
            if (statId < 0 || statId >= StatCount || handler == null)
            {
                return;
            }

            if (_statChangedHandlers != null)
            {
                _statChangedHandlers[statId] -= handler;
                _activeSubscriptionCount = Math.Max(0, _activeSubscriptionCount - 1);
                OnListenersChanged();
            }
        }

        /// <summary>
        /// 原生订阅指定属性区间数值发生变化时的通知 (支持 Min 与 Max 全面感知)
        /// </summary>
        /// <param name="statId">属性 ID</param>
        /// <param name="handler">区间变化回调 (旧区间, 新区间)</param>
        /// <param name="triggerImmediately">是否在订阅时以当前最新区间立即回调一次</param>
        /// <returns>用于注销订阅的 IDisposable 凭据</returns>
        public IDisposable SubscribeRange(int statId, StatRangeChangedHandler handler, bool triggerImmediately = false)
        {
            if (statId < 0 || statId >= StatCount || handler == null)
            {
                return null;
            }

            if (triggerImmediately)
            {
                var currentRange = GetRange(statId);
                handler.Invoke(currentRange, currentRange);
            }

            _rangeChangedHandlers[statId] += handler;
            _activeSubscriptionCount++;
            OnListenersChanged();

            return new RangeSubscriptionScope(this, statId, handler);
        }

        /// <summary>
        /// 取消对指定属性区间变化的订阅
        /// </summary>
        public void UnsubscribeRange(int statId, StatRangeChangedHandler handler)
        {
            if (statId < 0 || statId >= StatCount || handler == null)
            {
                return;
            }

            if (_rangeChangedHandlers != null)
            {
                _rangeChangedHandlers[statId] -= handler;
                _activeSubscriptionCount = Math.Max(0, _activeSubscriptionCount - 1);
                OnListenersChanged();
            }
        }

        public void Dispose()
        {
            StatUpdateDriver.Unregister(this);
            _onRecalculated = null;
            _activeSubscriptionCount = 0;
            _internalModifiers.OnChanged -= MarkDirty;
            _internalModifiers.Clear();
            ClearSources();
            _allSources.Clear();
            _conditionalModifiers.Clear();
            if (_statChangedHandlers != null)
            {
                Array.Clear(_statChangedHandlers, 0, _statChangedHandlers.Length);
            }
            if (_rangeChangedHandlers != null)
            {
                Array.Clear(_rangeChangedHandlers, 0, _rangeChangedHandlers.Length);
            }
            _slots = null;
            _previousRanges = null;
            _statChangedHandlers = null;
            _rangeChangedHandlers = null;
        }

        private void InitializePreviousRanges()
        {
            if (_previousRanges == null)
            {
                return;
            }

            var count = _previousRanges.Length;
            for (var i = 0; i < count; i++)
            {
                var def = _definitionGetter != null ? _definitionGetter(i) : StatDefinitionData.Default;
                _previousRanges[i] = new StatRange(def.DefaultBaseValue, def.DefaultBaseValueMax);
            }
        }

        private void EndBatchUpdate()
        {
            _batchDepth = Math.Max(0, _batchDepth - 1);
            if (_batchDepth == 0 && _isDirty)
            {
                if (_autoFlush && HasActiveListeners)
                {
                    StatUpdateDriver.RegisterDirty(this);
                }
            }
        }

        private void OnListenersChanged()
        {
            if (HasActiveListeners)
            {
                if (_isDirty && _autoFlush && _batchDepth == 0)
                {
                    StatUpdateDriver.RegisterDirty(this);
                }
            }
            else
            {
                StatUpdateDriver.Unregister(this);
            }
        }

        private static void InvokeSafe(StatChangedHandler handler, float oldValue, float newValue)
        {
            var list = handler.GetInvocationList();
            var count = list.Length;
            for (var i = 0; i < count; i++)
            {
                try
                {
                    ((StatChangedHandler)list[i])(oldValue, newValue);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[StatContainer] StatChangedHandler 回调执行异常: {ex}");
                }
            }
        }

        private static void InvokeSafe(StatRangeChangedHandler handler, StatRange oldValue, StatRange newValue)
        {
            var list = handler.GetInvocationList();
            var count = list.Length;
            for (var i = 0; i < count; i++)
            {
                try
                {
                    ((StatRangeChangedHandler)list[i])(oldValue, newValue);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[StatContainer] StatRangeChangedHandler 回调执行异常: {ex}");
                }
            }
        }

        public readonly struct BatchScope : IDisposable
        {
            private readonly StatContainer _container;

            public BatchScope(StatContainer container)
            {
                _container = container;
            }

            public void Dispose()
            {
                _container?.EndBatchUpdate();
            }
        }

        private sealed class ScalarSubscriptionScope : IDisposable
        {
            private StatContainer _container;
            private readonly int _statId;
            private StatChangedHandler _handler;

            public ScalarSubscriptionScope(StatContainer container, int statId, StatChangedHandler handler)
            {
                _container = container;
                _statId = statId;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_container != null && _handler != null)
                {
                    _container.Unsubscribe(_statId, _handler);
                    _container = null;
                    _handler = null;
                }
            }
        }

        private sealed class RangeSubscriptionScope : IDisposable
        {
            private StatContainer _container;
            private readonly int _statId;
            private StatRangeChangedHandler _handler;

            public RangeSubscriptionScope(StatContainer container, int statId, StatRangeChangedHandler handler)
            {
                _container = container;
                _statId = statId;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_container != null && _handler != null)
                {
                    _container.UnsubscribeRange(_statId, _handler);
                    _container = null;
                    _handler = null;
                }
            }
        }
    }
}
