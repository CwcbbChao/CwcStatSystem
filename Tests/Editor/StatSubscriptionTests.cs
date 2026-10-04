using System;
using System.Collections.Generic;
using Cwcbb.Tools.CwcStatSystem;
using NUnit.Framework;

namespace Cwcbb.Tools.CwcStatSystem.Tests
{
    [TestFixture]
    public class StatSubscriptionTests
    {
        [Test]
        public void Test_Subscribe_TriggersOnChange()
        {
            var container = new StatContainer(2, id => new StatDefinitionData(10f));
            var triggerCount = 0;
            var receivedOld = 0f;
            var receivedNew = 0f;

            using (container.Subscribe(0, (oldVal, newVal) =>
            {
                triggerCount++;
                receivedOld = oldVal;
                receivedNew = newVal;
            }))
            {
                Assert.AreEqual(0, triggerCount);

                // 添加 +5 的修饰符
                container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 5f));
                container.EnsureClean();

                Assert.AreEqual(1, triggerCount);
                Assert.AreEqual(10f, receivedOld, 0.001f);
                Assert.AreEqual(15f, receivedNew, 0.001f);

                // 未修改的属性 1 不应触发
                var triggerCount1 = 0;
                container.Subscribe(1, (o, n) => triggerCount1++);
                container.EnsureClean();
                Assert.AreEqual(0, triggerCount1);
            }
        }

        [Test]
        public void Test_Subscribe_TriggerImmediately()
        {
            var container = new StatContainer(1, id => new StatDefinitionData(20f));
            container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 10f));

            var triggerCount = 0;
            var receivedOld = 0f;
            var receivedNew = 0f;

            // triggerImmediately 应该以最新的 30f 立即回调
            using (container.Subscribe(0, (oldVal, newVal) =>
            {
                triggerCount++;
                receivedOld = oldVal;
                receivedNew = newVal;
            }, triggerImmediately: true))
            {
                Assert.AreEqual(1, triggerCount);
                Assert.AreEqual(30f, receivedOld, 0.001f);
                Assert.AreEqual(30f, receivedNew, 0.001f);
            }
        }

        [Test]
        public void Test_Unsubscribe_And_Dispose()
        {
            var container = new StatContainer(1, id => new StatDefinitionData(10f));
            var triggerCount = 0;

            StatChangedHandler handler = (oldVal, newVal) => triggerCount++;

            // 1. 显式退订测试
            container.Subscribe(0, handler);
            container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 2f));
            container.EnsureClean();
            Assert.AreEqual(1, triggerCount);

            container.Unsubscribe(0, handler);
            container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 3f));
            container.EnsureClean();
            Assert.AreEqual(1, triggerCount);

            // 2. IDisposable 作用域退订测试
            var subDisposable = container.Subscribe(0, handler);
            container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 5f));
            container.EnsureClean();
            Assert.AreEqual(2, triggerCount);

            subDisposable.Dispose();
            container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 5f));
            container.EnsureClean();
            Assert.AreEqual(2, triggerCount);
        }

        [Test]
        public void Test_BatchUpdate_DebounceEvents()
        {
            var container = new StatContainer(1, id => new StatDefinitionData(10f));
            var triggerCount = 0;
            var lastNewVal = 0f;

            container.Subscribe(0, (oldVal, newVal) =>
            {
                triggerCount++;
                lastNewVal = newVal;
            });

            // 连续添加 10 个 Modifier 在 Batch 作用域内
            using (container.BeginBatchUpdate())
            {
                for (var i = 0; i < 10; i++)
                {
                    container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 2f));
                }

                // Batch 作用域内，即使用户尝试 EnsureClean 也不应提前派发
                container.EnsureClean();
                Assert.AreEqual(0, triggerCount);
            }

            // 离开 Batch 作用域后执行 EnsureClean，仅派发 1 次最终值通知
            container.EnsureClean();
            Assert.AreEqual(1, triggerCount);
            Assert.AreEqual(30f, lastNewVal, 0.001f); // 10 + 10 * 2 = 30
        }

        [Test]
        public void Test_Synergy_CascadesToTargetStat()
        {
            // 属性 0: 力量 (基础值 10)
            // 属性 1: 生命值上限 (基础值 100)
            // 规则: 力量联动到生命上限 (每 1 点力量 + 2 点固定生命)
            var container = new StatContainer(2, id => id == 0 ? new StatDefinitionData(10f) : new StatDefinitionData(100f));
            container.AddModifier(new StatModifier(1, 0, ModifierType.FlatAdd, 2f));

            var hpChangeCount = 0;
            var receivedOldHp = 0f;
            var receivedNewHp = 0f;

            // 仅订阅生命值上限 (属性 1)
            using (container.Subscribe(1, (oldVal, newVal) =>
            {
                hpChangeCount++;
                receivedOldHp = oldVal;
                receivedNewHp = newVal;
            }))
            {
                // 首次结算生命值: 100 + 10 * 2 = 120
                container.EnsureClean();
                Assert.AreEqual(1, hpChangeCount);
                Assert.AreEqual(100f, receivedOldHp, 0.001f);
                Assert.AreEqual(120f, receivedNewHp, 0.001f);

                // 仅修改源属性 (力量 +10)
                container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 10f));
                container.EnsureClean();

                // 验证生命上限订阅者自动感知到了级联变动: 100 + 20 * 2 = 140
                Assert.AreEqual(2, hpChangeCount);
                Assert.AreEqual(120f, receivedOldHp, 0.001f);
                Assert.AreEqual(140f, receivedNewHp, 0.001f);
            }
        }

        [Test]
        public void Test_StatUpdateDriver_FlushAll()
        {
            var container = new StatContainer(1, id => new StatDefinitionData(50f));
            var triggerCount = 0;
            var lastVal = 0f;

            container.Subscribe(0, (oldVal, newVal) =>
            {
                triggerCount++;
                lastVal = newVal;
            });

            // 1. 默认 AutoFlush = true，变脏后通过 StatUpdateDriver.FlushAll() 触发刷新
            container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 10f));
            Assert.IsTrue(container.IsDirty);
            Assert.AreEqual(0, triggerCount);

            StatUpdateDriver.FlushAll();
            Assert.IsFalse(container.IsDirty);
            Assert.AreEqual(1, triggerCount);
            Assert.AreEqual(60f, lastVal, 0.001f);

            // 2. 测试 AutoFlush = false，不应向 StatUpdateDriver 报备
            container.AutoFlush = false;
            container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 10f));
            Assert.IsTrue(container.IsDirty);

            StatUpdateDriver.FlushAll();
            // 因为 AutoFlush 为 false，StatUpdateDriver.FlushAll 不会刷新该容器
            Assert.IsTrue(container.IsDirty);
            Assert.AreEqual(1, triggerCount);

            // 必须显式调用 EnsureClean 才会刷新
            container.EnsureClean();
            Assert.IsFalse(container.IsDirty);
            Assert.AreEqual(2, triggerCount);
            Assert.AreEqual(70f, lastVal, 0.001f);
        }

        [Test]
        public void Test_Tolerance_FiltersOutEpsilon()
        {
            var container = new StatContainer(1, id => new StatDefinitionData(10f));
            var triggerCount = 0;
            container.EnsureClean(); // 初始化为 10f

            container.Subscribe(0, (oldVal, newVal) => triggerCount++);

            // 微小变动 (小于 1e-5f)
            container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 1e-6f));
            container.EnsureClean();

            // 容差过滤，不应触发回调
            Assert.AreEqual(0, triggerCount);
        }

        [Test]
        public void Test_SubscribeRange_DetectsMaxOnlyChanges()
        {
            // 基础攻击力: 100 ~ 150
            var container = new StatContainer(1, id => new StatDefinitionData(100f, 150f));
            var rangeTriggerCount = 0;
            var scalarTriggerCount = 0;
            var lastReceivedOldRange = default(StatRange);
            var lastReceivedNewRange = default(StatRange);

            // 同时订阅区间与标量
            using (container.SubscribeRange(0, (oldRange, newRange) =>
            {
                rangeTriggerCount++;
                lastReceivedOldRange = oldRange;
                lastReceivedNewRange = newRange;
            }))
            using (container.Subscribe(0, (oldVal, newVal) =>
            {
                scalarTriggerCount++;
            }))
            {
                // 首次确保基准烘焙完毕
                container.EnsureClean();
                // 首次 EnsureClean 时，由于初始 _previousRanges 与基础值一致，不会触发多余通知
                Assert.AreEqual(0, rangeTriggerCount);
                Assert.AreEqual(0, scalarTriggerCount);

                // 添加仅改变上限的修饰符: Min +0, Max +50
                container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, new StatRange(0f, 50f)));
                container.EnsureClean();

                // 核心验证: 区间订阅者成功收到通知！标量订阅者不受骚扰
                Assert.AreEqual(1, rangeTriggerCount);
                Assert.AreEqual(100f, lastReceivedOldRange.Min, 0.001f);
                Assert.AreEqual(150f, lastReceivedOldRange.Max, 0.001f);
                Assert.AreEqual(100f, lastReceivedNewRange.Min, 0.001f);
                Assert.AreEqual(200f, lastReceivedNewRange.Max, 0.001f);

                Assert.AreEqual(0, scalarTriggerCount);

                // 再次修改，改变下限 Min +20
                container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 20f));
                container.EnsureClean();

                // 此时两者均应触发
                Assert.AreEqual(2, rangeTriggerCount);
                Assert.AreEqual(1, scalarTriggerCount);
                Assert.AreEqual(120f, lastReceivedNewRange.Min, 0.001f);
                Assert.AreEqual(220f, lastReceivedNewRange.Max, 0.001f);
            }
        }

        [Test]
        public void Test_NoSubscribers_DoesNotRegisterToDriver()
        {
            var container = new StatContainer(1, id => new StatDefinitionData(100f));

            // 无任何订阅者时添加修饰符
            container.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 50f));
            Assert.IsTrue(container.IsDirty);
            Assert.IsFalse(container.HasActiveListeners);
            Assert.IsFalse(container.IsInDriverQueue);

            // 执行集中帧末刷新，因为无订阅者，该容器不应被驱动刷新，保持纯惰性
            StatUpdateDriver.FlushAll();
            Assert.IsTrue(container.IsDirty);

            // 只有当有逻辑主动查询，或者有订阅者加入时才会按需计算
            var val = container.GetValue(0);
            Assert.IsFalse(container.IsDirty);
            Assert.AreEqual(150f, val, 0.001f);
        }
    }
}
