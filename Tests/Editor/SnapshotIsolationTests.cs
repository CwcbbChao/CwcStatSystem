using Cwcbb.Tools.CwcStatSystem;
using NUnit.Framework;

namespace Cwcbb.Tools.CwcStatSystem.Tests
{
    [TestFixture]
    public class SnapshotIsolationTests
    {
        private const int AttackStat = 0;
        private const ulong CritTag = 1UL << 0;
        private const ulong FrozenTag = 1UL << 1;

        [Test]
        public void Test_Snapshot_MaintainsTemporalIsolation_WhenCharacterChangesLater()
        {
            var container = new StatContainer(1, id => new StatDefinitionData(100f));

            // 角色常态穿戴一把强力神兵: 基础攻击力 +400, 暴击时造成 1.5 倍伤害, 目标冻结增伤 +30%
            var weapon = new ModifierCollection();
            weapon.AddModifier(new StatModifier(AttackStat, ModifierType.FlatAdd, 400f));
            weapon.AddModifier(new StatModifier(AttackStat, ModifierType.MoreMult, 0.5f, CritTag));
            weapon.AddModifier(new StatModifier(AttackStat, ModifierType.MoreMult, 0.3f, FrozenTag));

            container.AddSource(weapon);

            // 施法瞬间：常态攻击力为 500，且施法时判定触发了暴击
            Assert.AreEqual(500f, container.GetValue(AttackStat), 0.001f);
            var snapshot = container.CreateSnapshot(castTagMask: CritTag);

            // 施法后发生突变：角色脱下武器，并被敌人挂上致命虚弱 Debuff (攻击力降为 10 点)
            container.RemoveSource(weapon);
            var curse = new ModifierCollection();
            curse.AddModifier(new StatModifier(AttackStat, ModifierType.Override, 10f));
            container.AddSource(curse);

            // 验证此时角色自身攻击力已经暴跌至 10 点
            Assert.AreEqual(10f, container.GetValue(AttackStat), 0.001f);

            // 验证空中的飞行法术快照：
            // 1. 无额外命中 Tag 时：严格保持施法时刻的 500 点底数与暴击状态：500 * 1.5 = 750！
            var snapshotDmg1 = snapshot.GetValue(AttackStat);
            Assert.AreEqual(750f, snapshotDmg1, 0.001f);

            // 2. 命中冻结目标时：500 * 1.5 (暴击) * 1.3 (冻结) = 975！
            var snapshotDmg2 = snapshot.GetValue(AttackStat, FrozenTag);
            Assert.AreEqual(975f, snapshotDmg2, 0.001f);

            // 3. 回收快照
            snapshot.Dispose();
            Assert.IsTrue(snapshot.IsDisposed);
        }
    }
}
