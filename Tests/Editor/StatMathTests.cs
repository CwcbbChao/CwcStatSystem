using Cwcbb.Tools.CwcStatSystem;
using NUnit.Framework;

namespace Cwcbb.Tools.CwcStatSystem.Tests
{
    [TestFixture]
    public class StatMathTests
    {
        private const int AttackStat = 0;
        private const int SpeedStat = 1;

        [Test]
        public void Test_CoreMultipliers_CalculatesCorrectly()
        {
            // 公式: (Base + Flat) * (1 + PercentAdd) * MoreMult + PostFlatAdd
            // (100 + 50) * (1 + 0.40) * 1.5 + 20
            // = 150 * 1.4 * 1.5 + 20 = 210 * 1.5 + 20 = 315 + 20 = 335
            var container = new StatContainer(2, id => new StatDefinitionData(id == AttackStat ? 100f : 10f));

            var collection = new ModifierCollection();
            collection.AddModifier(new StatModifier(AttackStat, ModifierType.FlatAdd, 50f));
            collection.AddModifier(new StatModifier(AttackStat, ModifierType.PercentAdd, 0.40f));
            collection.AddModifier(new StatModifier(AttackStat, ModifierType.MoreMult, 0.5f));
            collection.AddModifier(new StatModifier(AttackStat, ModifierType.PostFlatAdd, 20f));

            container.AddSource(collection);

            var result = container.GetValue(AttackStat);
            Assert.AreEqual(335f, result, 0.001f);
        }

        [Test]
        public void Test_DirectMult_MultipliesDirectlyAndCombinesWithMoreMult()
        {
            // 公式: (Base + Flat) * (1 + PercentAdd) * (1 + MoreMult) * DirectMult + PostFlatAdd
            // (100 + 0) * (1 + 0) * (1 + 0.20) * 1.5 = 100 * 1.2 * 1.5 = 180
            var container = new StatContainer(2, id => new StatDefinitionData(100f));

            var collection = new ModifierCollection();
            collection.AddModifier(new StatModifier(AttackStat, ModifierType.MoreMult, 0.20f));   // 增量独立乘区: * (1 + 0.20)
            collection.AddModifier(new StatModifier(AttackStat, ModifierType.DirectMult, 1.5f));  // 直接独立乘区: * 1.5

            container.AddSource(collection);

            var result = container.GetValue(AttackStat);
            Assert.AreEqual(180f, result, 0.001f);
        }

        [Test]
        public void Test_ConditionalModifier_OnlyAppliesWhenTagMatches()
        {
            const ulong critTag = 1UL << 0;
            const ulong frozenTag = 1UL << 1;

            var container = new StatContainer(2, id => new StatDefinitionData(100f));

            var collection = new ModifierCollection();
            // 常态: +50 点伤
            collection.AddModifier(new StatModifier(AttackStat, ModifierType.FlatAdd, 50f));
            // 暴击条件: 造成 1.5 倍伤害 (即 +50% 独立增益)
            collection.AddModifier(new StatModifier(AttackStat, ModifierType.MoreMult, 0.5f, critTag));
            // 暴击 + 冻结条件: 额外附加 100 点伤害
            collection.AddModifier(new StatModifier(AttackStat, ModifierType.FlatAdd, 100f, critTag | frozenTag));

            container.AddSource(collection);

            // 1. 无 Tag 访问常态: 100 + 50 = 150
            Assert.AreEqual(150f, container.GetValue(AttackStat), 0.001f);

            // 2. 仅暴击: (100 + 50) * 1.5 = 225
            Assert.AreEqual(225f, container.GetValue(AttackStat, critTag), 0.001f);

            // 3. 暴击 + 冻结: (100 + 50 + 100) * 1.5 = 375
            Assert.AreEqual(375f, container.GetValue(AttackStat, critTag | frozenTag), 0.001f);

            // 4. 仅冻结 (不满足暴击要求): 仍然只有常态 150
            Assert.AreEqual(150f, container.GetValue(AttackStat, frozenTag), 0.001f);

            // 5. 再次无 Tag 访问: 确保底层常态未被污染
            Assert.AreEqual(150f, container.GetValue(AttackStat), 0.001f);
        }

        [Test]
        public void Test_Override_TakeMin_TakesStricterLimit()
        {
            // 移速默认为 TakeMin 策略 (小值优先限制/惩罚)
            var container = new StatContainer(2, id => new StatDefinitionData(10f, StatOverridePolicy.TakeMin));

            var collectionA = new ModifierCollection();
            collectionA.AddModifier(new StatModifier(SpeedStat, ModifierType.Override, 5f));

            var collectionB = new ModifierCollection();
            // 更严苛的定身/深度减速 (设为 2)
            collectionB.AddModifier(new StatModifier(SpeedStat, ModifierType.Override, 2f));

            container.AddSource(collectionA);
            Assert.AreEqual(5f, container.GetValue(SpeedStat), 0.001f);

            // 同时生效时，小值 2 严格压制 5
            container.AddSource(collectionB);
            Assert.AreEqual(2f, container.GetValue(SpeedStat), 0.001f);

            // 移除最严苛的限制后，恢复次严格的 5
            container.RemoveSource(collectionB);
            Assert.AreEqual(5f, container.GetValue(SpeedStat), 0.001f);
        }

        [Test]
        public void Test_Override_TakeMax_TakesHigherValue()
        {
            // 减伤/保底属性配置为 TakeMax 策略 (大值优先保底)
            var container = new StatContainer(2, id => new StatDefinitionData(10f, StatOverridePolicy.TakeMax));

            var collection = new ModifierCollection();
            collection.AddModifier(new StatModifier(SpeedStat, ModifierType.Override, 15f));
            collection.AddModifier(new StatModifier(SpeedStat, ModifierType.Override, 40f));

            container.AddSource(collection);
            Assert.AreEqual(40f, container.GetValue(SpeedStat), 0.001f);
        }

        [Test]
        public void Test_StatModifierConfig_CreatesModifierCorrectly()
        {
            // 验证动静分离配置模板
            var config = new StatModifierConfig(AttackStat, ModifierType.PercentAdd, 1UL << 2);
            var mod = config.ToModifier(0.35f);

            Assert.AreEqual(AttackStat, mod.TargetStatId);
            Assert.AreEqual(ModifierType.PercentAdd, mod.ModifyType);
            Assert.AreEqual(0.35f, mod.Value.Min, 0.001f);
            Assert.AreEqual(1UL << 2, mod.RequiredTagMask);
            Assert.AreSame(config, mod.Config);
        }

        [Test]
        public void Test_RangeValues_CalculatesMinMaxAndRollsCorrectly()
        {
            // 暗黑like经典场景:
            // 基础点伤 10
            // 武器提供 15 ~ 30 物理伤害
            // 戒指提供 5 ~ 10 物理伤害
            // 天赋提供 +50% 物理伤害增加 (PercentAdd)
            // 独立增伤宝石提供 +20% 独立增伤 (MoreMult: 0.2)
            //
            // 计算过程:
            // Min = (10 + 15 + 5) * (1 + 0.5) * (1 + 0.2) = 30 * 1.5 * 1.2 = 54
            // Max = (10 + 30 + 10) * (1 + 0.5) * (1 + 0.2) = 50 * 1.5 * 1.2 = 90
            var container = new StatContainer(2, id => new StatDefinitionData(10f));

            var gear = new ModifierCollection();
            gear.AddModifier(new StatModifier(AttackStat, ModifierType.FlatAdd, 15f, 30f)); // 范围点伤
            gear.AddModifier(new StatModifier(AttackStat, ModifierType.FlatAdd, 5f, 10f));   // 范围点伤
            gear.AddModifier(new StatModifier(AttackStat, ModifierType.PercentAdd, 0.5f));    // 标量百分比
            gear.AddModifier(new StatModifier(AttackStat, ModifierType.MoreMult, 0.2f));      // 标量独立乘区

            container.AddSource(gear);

            var range = container.GetRange(AttackStat);
            Assert.AreEqual(54f, range.Min, 0.001f);
            Assert.AreEqual(90f, range.Max, 0.001f);
            Assert.AreEqual(54f, container.GetValue(AttackStat), 0.001f); // 标量访问默认取 Min

            // 摇点验证: 连续摇点 50 次，结果必须严格落在 [54, 90] 之内
            for (var i = 0; i < 50; i++)
            {
                var rolled = container.RollValue(AttackStat);
                Assert.IsTrue(rolled >= 54f && rolled <= 90f, $"Rolled value {rolled} out of [54, 90]");
            }
        }

        [Test]
        public void Test_StatModifierConfig_WithRange_ConstructsCorrectly()
        {
            // 构造通用配置模板
            var config = new StatModifierConfig(AttackStat, ModifierType.FlatAdd);

            // 传入双参数构造范围修饰符
            var mod = config.ToModifier(25f, 60f);
            Assert.AreEqual(25f, mod.MinValue, 0.001f);
            Assert.AreEqual(60f, mod.MaxValue, 0.001f);
            Assert.IsTrue(mod.Value.IsRange);

            // 传入列表构造
            var listMod = config.ToModifier(new float[] { 30f, 80f });
            Assert.AreEqual(30f, listMod.MinValue, 0.001f);
            Assert.AreEqual(80f, listMod.MaxValue, 0.001f);
        }
    }
}
