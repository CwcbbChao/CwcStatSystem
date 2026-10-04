using Cwcbb.Tools.CwcStatSystem;
using NUnit.Framework;

namespace Cwcbb.Tools.CwcStatSystem.Tests
{
    [TestFixture]
    public class InternalModifierCollectionTests
    {
        private const int AttackStat = 0;
        private const int ArmorStat = 1;

        [Test]
        public void Test_InternalModifiers_DirectAddAndRemove_WorksCorrectly()
        {
            var container = new StatContainer(2, id => new StatDefinitionData(100f));

            var mod1 = new StatModifier(AttackStat, ModifierType.FlatAdd, 20f);
            var mod2 = new StatModifier(AttackStat, ModifierType.PercentAdd, 0.5f);

            // 1. 直接向容器添加单个修饰符
            container.AddModifier(mod1);
            container.AddModifier(mod2);

            // (100 + 20) * 1.5 = 180
            Assert.AreEqual(180f, container.GetValue(AttackStat), 0.001f);
            Assert.AreEqual(2, container.Modifiers.Count);

            // 2. 移除单个修饰符
            var removed = container.RemoveModifier(mod2);
            Assert.IsTrue(removed);
            // 100 + 20 = 120
            Assert.AreEqual(120f, container.GetValue(AttackStat), 0.001f);
            Assert.AreEqual(1, container.Modifiers.Count);

            // 3. 清空所有内部修饰符
            container.ClearModifiers();
            Assert.AreEqual(100f, container.GetValue(AttackStat), 0.001f);
            Assert.AreEqual(0, container.Modifiers.Count);
        }

        [Test]
        public void Test_InternalModifiers_ShareToTarget_WorksCorrectly()
        {
            // 容器 A (如光环源 / 主人)
            var auraProvider = new StatContainer(2, id => new StatDefinitionData(50f));
            // 容器 B (如受影响队友 / 召唤物)
            var receiver = new StatContainer(2, id => new StatDefinitionData(100f));

            var auraMod = new StatModifier(AttackStat, ModifierType.FlatAdd, 30f);
            auraProvider.AddModifier(auraMod);

            // 1. 将 auraProvider 的内部集合作为一个整体生效给 receiver
            auraProvider.ApplyModifiersTo(receiver);

            // receiver 此时受到 100 + 30 = 130
            Assert.AreEqual(130f, receiver.GetValue(AttackStat), 0.001f);

            // 2. auraProvider 发生动态改变 (例如技能升级，光环增加百分比加成)
            var extraAuraMod = new StatModifier(AttackStat, ModifierType.PercentAdd, 0.2f);
            auraProvider.AddModifier(extraAuraMod);

            // receiver 自动感应到变化并重算: (100 + 30) * 1.2 = 156
            Assert.AreEqual(156f, receiver.GetValue(AttackStat), 0.001f);

            // 3. 撤销生效
            auraProvider.RemoveModifiersFrom(receiver);

            // receiver 恢复原生基础值 100
            Assert.AreEqual(100f, receiver.GetValue(AttackStat), 0.001f);
        }

        [Test]
        public void Test_ClearSources_DoesNotClearInternalModifiers()
        {
            var container = new StatContainer(2, id => new StatDefinitionData(100f));

            // 添加内部修饰符
            container.AddModifier(new StatModifier(AttackStat, ModifierType.FlatAdd, 25f));

            // 挂载外部装备修饰符集合
            var externalEquip = new ModifierCollection();
            externalEquip.AddModifier(new StatModifier(AttackStat, ModifierType.FlatAdd, 50f));
            container.AddSource(externalEquip);

            // 100 + 25 + 50 = 175
            Assert.AreEqual(175f, container.GetValue(AttackStat), 0.001f);
            Assert.AreEqual(1, container.Sources.Count); // Sources 仅暴露外部集合

            // 清空所有外部源 (模拟一键卸载所有装备)
            container.ClearSources();

            // 外部源已被清空，但自带的内部修饰符依然生效: 100 + 25 = 125
            Assert.AreEqual(125f, container.GetValue(AttackStat), 0.001f);
            Assert.AreEqual(0, container.Sources.Count);
            Assert.AreEqual(1, container.Modifiers.Count);
        }

        [Test]
        public void Test_ApplyModifiersTo_Self_SafelyIgnored()
        {
            var container = new StatContainer(2, id => new StatDefinitionData(100f));
            container.AddModifier(new StatModifier(AttackStat, ModifierType.FlatAdd, 20f));

            // 向自己应用应被安全忽略，防止造成循环引用或双倍累加
            container.ApplyModifiersTo(container);

            Assert.AreEqual(120f, container.GetValue(AttackStat), 0.001f);
            Assert.AreEqual(0, container.Sources.Count);
        }

        [Test]
        public void Test_SkillInheritsCharacterModifiers_AndCombinesWithSelfModifiers()
        {
            // 模拟角色容器与技能容器，基础伤害定义均为 0
            var ownerContainer = new StatContainer(2, id => new StatDefinitionData(0f));
            var skillContainer = new StatContainer(2, id => new StatDefinitionData(0f));

            // 1. 技能自身的基础伤害修饰符 (如配置表基础攻击力 50)
            skillContainer.AddModifier(new StatModifier(AttackStat, ModifierType.FlatAdd, 50f));
            Assert.AreEqual(50f, skillContainer.GetValue(AttackStat), 0.001f);

            // 2. 角色自身带属性 (如角色基础力量加成 20 点攻击力，10% 伤害加成)
            ownerContainer.AddModifier(new StatModifier(AttackStat, ModifierType.FlatAdd, 20f));
            ownerContainer.AddModifier(new StatModifier(AttackStat, ModifierType.PercentAdd, 0.10f));

            // 3. 技能绑定主人：继承角色属性
            ownerContainer.ApplyModifiersTo(skillContainer);

            // 复合计算：FlatAdd = 50 + 20 = 70; PercentAdd = 10% -> 70 * 1.10 = 77
            Assert.AreEqual(77f, skillContainer.GetValue(AttackStat), 0.001f);

            // 4. 角色在运行时获得额外装备或 Buff (增加 50% 伤害)
            var buffMod = new StatModifier(AttackStat, ModifierType.PercentAdd, 0.50f);
            ownerContainer.AddModifier(buffMod);

            // 技能自动联动感知：FlatAdd = 70; PercentAdd = 10% + 50% = 60% -> 70 * 1.60 = 112
            Assert.AreEqual(112f, skillContainer.GetValue(AttackStat), 0.001f);

            // 5. 角色 Buff 结束，移除 Buff
            ownerContainer.RemoveModifier(buffMod);
            Assert.AreEqual(77f, skillContainer.GetValue(AttackStat), 0.001f);

            // 6. 技能解除绑定 (或角色死亡/换装)
            ownerContainer.RemoveModifiersFrom(skillContainer);
            // 技能恢复仅由自身修饰符决定
            Assert.AreEqual(50f, skillContainer.GetValue(AttackStat), 0.001f);
        }
    }
}
