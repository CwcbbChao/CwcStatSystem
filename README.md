# CwcStatSystem - 高性能 RPG 属性与数值计算系统

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Custom%20(Free%20for%20Games)-blue.svg)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/CwcbbChao/CwcStatSystem/pulls)

[English](README_EN.md) | **简体中文**

---

## 它是用来做什么的？

在制作 RPG、ARPG、肉鸽或动作游戏时，角色的属性与伤害计算经常面临几大头疼痛点：
1. **加法和乘法谁先谁后？公式容易算乱**：装备加基础点伤、天赋加百分比、狂暴 Buff 还要独立连乘（类似《流放之路》PoE 的 More 乘区），自己写很容易把乘区顺序搞错。
2. **武器攻击力是浮动的（如 15~35），很难算**：很多传统属性系统只能算一个固定数字。遇到“15~35 攻击力再吃 30% 加成”时，只能手动拆成 Min 和 Max 算两次，极其繁琐。
3. **属性联动容易死循环**：“每 10 点力量增加 2 点攻击力”、“最大生命值的 5% 转化为护甲”，自己写递归计算稍不注意就会死循环卡死游戏。
4. **换装备掉帧与垃圾回收（GC）**：用 Dictionary 存属性不仅查询慢，每次穿脱装备几十个词条变动，还会产生堆内存碎片引发掉帧。
5. **飞在空中的子弹数值被改**：法师扔出一发火球，火球还在半空飞，法师身上的增伤 Buff 结束了，结果飞在空中的火球伤害跟着缩水了。

`CwcStatSystem` 是一个专为 Unity 打造的**高性能、现代化、零垃圾回收（0-GC）**的 RPG 属性计算插件。它把复杂的数值乘区、浮动区间与属性联动包装成极简直观的 API，开箱即用。

---

## 核心功能解决的痛点

### 1. 正统 RPG 六大标准乘区（自动分层，绝不算错）
系统内部自动按照正统 RPG 的黄金公式分层计算，开发者只需给属性挂词条，公式全自动生效：
- **基础加值（FlatAdd / 点伤）**：先累加到底数上（例如：攻击力 +10）；
- **加算百分比（PercentAdd / 基础百分比）**：所有来源累加后统一放大（例如：装备 +15% 攻击、天赋 +20% 攻击，合并按 +35% 放大）；
- **独立乘算（MoreMult / 独立额外增益）**：类似 PoE 的 More 乘区，多个独立增益项连乘（例如：狂暴状态伤害 x1.2、暴击增伤 x1.5，相互连乘避免被稀释）；
- **后置固定值（PostFlatAdd）**：在所有乘区放大完后的最终结果上直接累加，不吃百分比二次加成；
- **强制覆盖（Override）**：绝对值锁定（例如：被冰冻定身时移动速度强制设为 0，支持“小值优先”或“大值优先”策略）。

### 2. 原生支持浮动范围（如 15~35 伤害，区间同步放大）
- 系统原生支持区间数值（`StatRange`）。
- 武器基础伤害设为 `[15, 35]`，当吃到 50% 增伤加成时，系统自动将两端同步计算为 `[22.5, 52.5]`，无需写两套代码。

### 3. 复杂的属性联动转换（力量转攻击、生命转护甲，绝不死循环）
- 支持“每 1 点力量转化为 0.5 点攻击力”、“最大生命值的 10% 转化为护甲”。
- 内部采用极速两趟正交线性扫描，天然杜绝无限递归和死循环，计算速度快至纳秒级。

### 4. 0-GC 极速性能与惰性求值（穿脱装备绝不卡顿）
- 内部采用扁平化一维纯数组存储，没有 Dictionary 哈希寻址开销。
- 穿脱装备或叠 Buff 时，只打轻量“脏标记”，**只有真正要读取该数值时才按需计算**。一瞬间换满一身装备，也完全不会掉帧或产生堆内存垃圾。

### 5. 快照隔离（让发射出去的子弹锁定出手瞬间的伤害）
- 支持一键导出只读的数值快照（`StatSnapshot`）。
- 子弹或技能实体生成时携带该快照，之后人物身上的 Buff 无论是消失、强化还是被驱散，都不会影响飞在空中的这颗子弹。

### 6. 条件增伤位掩码（对流血敌人增伤、低血量增伤）
- 支持词条附加条件（例如：“对被冰冻的目标暴击率 +20%”）。
- 通过纯位运算（Tag Mask）零成本匹配，不消耗任何多余性能。

### 7. 一键生成强类型代码（告别拼错字符串的恶梦）
- 在策划配置表（`StatConfigSO`）里填好游戏里有哪些属性（如 Health, Attack, Defense, MoveSpeed）。
- 点击 **Generate Code** 按钮，系统自动生成对应的 C# Enum 与属性注册表！
- 在代码里可以直接写 `Stats.Attack`，拥有完整的代码自动补全与编译期安全检查，告别拼写手滑。

---

## 安装方式

### 方式 A：通过 Unity Package Manager (推荐)
1. 打开 Unity 编辑器菜单栏：`Window` -> `Package Manager`。
2. 点击左上角 `+` 号 -> 选择 **Add package from git URL...**。
3. 填入仓库地址：
   ```text
   https://github.com/CwcbbChao/CwcStatSystem.git
   ```
4. 点击 **Add** 等待安装完成。

### 方式 B：直接复制代码
将 `CwcStatSystem` 文件夹直接放入项目的 `Assets/` 或 `Packages/` 目录下即可。

---

## 快速上手（极简三步法）

### 第一步：在编辑器中配置属性表并生成代码
1. 在 Project 视图右键：`Create -> Cwcbb -> Stat System -> Stat Config`，新建一份配置（如 `PlayerStatsConfig.asset`）；
2. 添加你的游戏属性（例如：生命值 Health、基础攻击 Attack、移动速度 MoveSpeed 等）；
3. 点击 Inspector 顶部的 **`Generate Code`** 按钮，系统会自动生成强类型的属性枚举代码。

---

### 第二步：在角色身上初始化属性容器
```csharp
using UnityEngine;
using Cwcbb.Tools.CwcStatSystem;

public class CharacterStatsComponent : MonoBehaviour
{
    private StatContainer _stats;

    private void Awake()
    {
        // 1. 初始化容器（传入属性总数）
        _stats = new StatContainer(numStats: 10);

        // 2. 监听属性数值变化（例如生命值变动更新 UI）
        _stats.SubscribeToStatChanged(0, (statId, oldValue, newValue) =>
        {
            Debug.Log($"属性变化: {oldValue} -> {newValue}");
        });
    }

    public StatContainer Stats => _stats;
}
```

---

### 第三步：穿装备、叠 Buff 与读取数值！

#### 1. 穿装备添加词条
```csharp
// 穿上一把大剑：基础物理点伤 +15
_stats.AddModifier(new StatModifier(
    targetStatId: 0, // 攻击力
    value: 15f,
    modifyType: ModifierType.FlatAdd,
    source: swordItem
));

// 穿上一枚狂暴戒指：攻击力百分比 +25%
_stats.AddModifier(new StatModifier(
    targetStatId: 0,
    value: 0.25f,
    modifyType: ModifierType.PercentAdd,
    source: ringItem
));

// 激活嗜血 Buff：独立额外放大 x1.3 (More 乘区)
_stats.AddModifier(new StatModifier(
    targetStatId: 0,
    value: 0.30f,
    modifyType: ModifierType.MoreMult,
    source: bloodlustBuff
));
```

#### 2. 读取当前计算后的最终数值
```csharp
// 读取单值（极速常态缓存读取，零纳秒开销）：
float finalAttack = _stats.GetStatValue(0);

// 读取区间范围（例如 20~45 点浮动伤害）：
StatRange attackRange = _stats.GetStatRange(0);
Debug.Log($"当前攻击力范围: {attackRange.Min} ~ {attackRange.Max}");
```

#### 3. 脱下装备（一键清理来源的所有词条）
```csharp
// 脱下戒指，自动移除该物品贡献的所有词条并重新计算：
_stats.RemoveModifiersFromSource(ringItem);
```

---

## 六大乘区公式人话对照表

| 乘区类型 | 对应常识 | 计算规则 | 常见应用场景 |
| :--- | :--- | :--- | :--- |
| **`FlatAdd`** | **基础加值 / 点伤** | 累加到基础底数：`(Base + Flat)` | 武器攻击力 +15、直接力量 +5 |
| **`PercentAdd`** | **基础百分比 (Increased)** | 所有百分比相加后放大：`x (1 + Sum%)` | 装备词条 "攻击力提高 15%"、天赋 "+20%" |
| **`MoreMult`** | **独立额外乘算 (More)** | 独立相乘，不被稀释：`x (1 + More1) x (1 + More2)` | 狂暴 Buff 伤害增幅、暴击额外倍率 |
| **`DirectMult`** | **直接乘数 (Direct)** | 乘积直接相乘：`x Value` | 难度系数削弱、虚弱减伤倍率 |
| **`PostFlatAdd`** | **后置固定加值** | 乘完后再加，不被二次放大：`+ PostFlat` | 最终真伤固伤、固定防护盾量 |
| **`Override`** | **强制绝对值锁定** | 跳过加乘，直接取该值 | 被冰冻定身移速设为 0、血量锁定为 1 |

---

## 许可协议与商用说明

- 核心代码遵循 [Cwc Tools Public License (Source-Available)](LICENSE) 许可协议：
  - **开发游戏（End Products）**：**个人或商业游戏项目均可免费使用，免版税（Royalty-Free）**，无需支付任何费用。
  - **二次转售限制**：严禁将本插件或修改版本作为独立的资产包、开发工具、插件包发布到 Asset Store 或其它平台转售。
- 详细说明请查阅 [Third-Party Notices.txt](Third-Party%20Notices.txt)。
