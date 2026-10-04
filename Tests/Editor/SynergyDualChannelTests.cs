using Cwcbb.Tools.CwcStatSystem;
using NUnit.Framework;

namespace Cwcbb.Tools.CwcStatSystem.Tests
{
    [TestFixture]
    public class SynergyDualChannelTests
    {
        private const int StrengthStat = 0;
        private const int PhysicalDamageStat = 1;

        [Test]
        public void Test_SingleSynergy_DerivesFromNativeCorrectly()
        {
            // 力量基础 100，装备给力量 +50 点伤，力量自身 PercentAdd +20%
            // 力量 NativeValue = (100 + 50) * (1 + 0.2) = 180
            // 联动修饰符: 每 1 点力量，增加 0.01 (+1%) 物理伤害 PercentAdd
            // 物理伤害基础 200
            // 最终物理伤害 = 200 * (1 + 180 * 0.01) = 200 * 2.8 = 560
            var container = new StatContainer(2, id =>
            {
                if (id == StrengthStat) return new StatDefinitionData(100f);
                return new StatDefinitionData(200f);
            });

            var equip = new ModifierCollection();
            equip.AddModifier(new StatModifier(StrengthStat, ModifierType.FlatAdd, 50f));
            equip.AddModifier(new StatModifier(StrengthStat, ModifierType.PercentAdd, 0.20f));
            // 联动词缀直接通过统一的 StatModifier 挂载在装备上！
            equip.AddModifier(new StatModifier(PhysicalDamageStat, StrengthStat, ModifierType.PercentAdd, 0.01f));

            container.AddSource(equip);

            Assert.AreEqual(180f, container.GetValue(StrengthStat), 0.001f);
            Assert.AreEqual(560f, container.GetValue(PhysicalDamageStat), 0.001f);
        }

        [Test]
        public void Test_MutualSynergy_DoesNotDeadlockOrInfinitelyScale()
        {
            // 极端测试：双向互为依赖
            // 词缀 1: 力量 -> 伤害增加 (每点力量 +1% 伤害加算)
            // 词缀 2: 伤害 -> 力量增加 (每 10 点伤害 +1 点力量，即 0.1 比例基础值)
            //
            // 初始原生状态:
            // 力量原生基础 = 100
            // 伤害原生基础 = 200
            //
            // 在正交双通道铁律下:
            // 词缀 1 只读取力量原生值 100 -> 注入伤害协同 PercentAdd = 100 * 0.01 = 100% (+1.0)
            // 词缀 2 只读取伤害原生值 200 -> 注入力量协同 FlatAdd = 200 * 0.1 = 20 点
            //
            // 结算最终结果:
            // 最终力量 = 100 (原生) + 20 (协同) = 120
            // 最终伤害 = 200 (原生) * (1 + 100% [协同]) = 400
            //
            // 绝无死循环，瞬间收敛于闭式解析解！
            var container = new StatContainer(2, id =>
            {
                if (id == StrengthStat) return new StatDefinitionData(100f);
                return new StatDefinitionData(200f);
            });

            container.AddModifier(new StatModifier(PhysicalDamageStat, StrengthStat, ModifierType.PercentAdd, 0.01f));
            container.AddModifier(new StatModifier(StrengthStat, PhysicalDamageStat, ModifierType.FlatAdd, 0.1f));

            var strength = container.GetValue(StrengthStat);
            var damage = container.GetValue(PhysicalDamageStat);

            Assert.AreEqual(120f, strength, 0.001f);
            Assert.AreEqual(400f, damage, 0.001f);
        }

        [Test]
        public void Test_SynergyMoreMult_ScalesIndependently()
        {
            // 测试力量联动独立增益乘区 (MoreMult):
            // 力量基础 100
            // 物理伤害基础 200，自身带有 50% 加算增伤 (PercentAdd: 0.5)
            // 传奇词缀: 每 1 点力量，获得 0.01 (1%) 独立物理增伤 (MoreMult)
            // 动态增量 = 100 * 0.01 = 1.0 (即 +100% 独立增益，乘数按 1 + 1.0 = 2.0 连乘)
            // 最终物理伤害 = 200 * (1 + 0.5) * (1 + 1.0) = 200 * 1.5 * 2.0 = 600
            var container = new StatContainer(2, id =>
            {
                if (id == StrengthStat) return new StatDefinitionData(100f);
                return new StatDefinitionData(200f);
            });

            container.AddModifier(new StatModifier(PhysicalDamageStat, ModifierType.PercentAdd, 0.5f));
            container.AddModifier(new StatModifier(PhysicalDamageStat, StrengthStat, ModifierType.MoreMult, 0.01f));

            Assert.AreEqual(600f, container.GetValue(PhysicalDamageStat), 0.001f);
        }
    }
}
