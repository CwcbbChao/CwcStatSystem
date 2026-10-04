using Cwcbb.Tools.CwcStatSystem;
using NUnit.Framework;

namespace Cwcbb.Tools.CwcStatSystem.Tests
{
    [TestFixture]
    public class LazyDirtyFlagTests
    {
        [Test]
        public void Test_LazyDirtyFlag_MergesMultipleWritesIntoSingleEvaluation()
        {
            var container = new StatContainer(2, id => new StatDefinitionData(10f));
            var recalculateCount = 0;
            container.OnRecalculated += () => recalculateCount++;

            // 1. 初始状态为脏，但尚未发生任何实际重算
            Assert.IsTrue(container.IsDirty);
            Assert.AreEqual(0, recalculateCount);

            // 2. 连续模拟一键穿戴 10 件装备
            for (var i = 0; i < 10; i++)
            {
                var equip = new ModifierCollection();
                equip.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 5f));
                container.AddSource(equip);
            }

            // 在未调用任何 GetValue 之前，计算次数严格保持为 0！
            Assert.IsTrue(container.IsDirty);
            Assert.AreEqual(0, recalculateCount);

            // 3. 第一次调用 GetValue，触发仅且仅有 1 次真正的重算
            var val = container.GetValue(0);
            Assert.AreEqual(60f, val, 0.001f); // 10 + 10 * 5 = 60
            Assert.IsFalse(container.IsDirty);
            Assert.AreEqual(1, recalculateCount);

            // 4. 重复高频读取 100 次（模拟 UI 每帧刷新、跑图每帧读移速）
            for (var i = 0; i < 100; i++)
            {
                var readAgain = container.GetValue(0);
                Assert.AreEqual(60f, readAgain, 0.001f);
            }

            // 验证重算次数依然严格为 1，没有任何冗余计算！
            Assert.AreEqual(1, recalculateCount);
        }

        [Test]
        public void Test_BatchScope_DefersEvaluationUntilExplicitQuery()
        {
            var container = new StatContainer(2, id => new StatDefinitionData(10f));
            var recalculateCount = 0;
            container.OnRecalculated += () => recalculateCount++;

            using (container.BeginBatchUpdate())
            {
                for (var i = 0; i < 5; i++)
                {
                    var equip = new ModifierCollection();
                    equip.AddModifier(new StatModifier(0, ModifierType.FlatAdd, 10f));
                    container.AddSource(equip);
                }

                // 批处理作用域内未显式查询前，重算次数为 0
                Assert.AreEqual(0, recalculateCount);
            }

            // 离开作用域依然保持惰性，直到真正查询
            Assert.AreEqual(0, recalculateCount);
            var result = container.GetValue(0);
            Assert.AreEqual(60f, result, 0.001f);
            Assert.AreEqual(1, recalculateCount);
        }
    }
}
