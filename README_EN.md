# CwcStatSystem - High-Performance RPG Stat & Attribute Calculation System

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Custom%20(Free%20for%20Games)-blue.svg)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/CwcbbChao/CwcStatSystem/pulls)

**English** | [简体中文](README.md)

---

## What Does It Do?

When creating RPGs, ARPGs, Roguelites, or action combat games, character stat calculation often introduces frustrating challenges:
1. **Order of Operations Confusion**: Base weapon damage, talent percentages, and berserk buffs with independent multipliers (similar to *Path of Exile's* "More" modifier) are easy to mix up when coded by hand.
2. **Floating Damage Ranges (e.g., 15~35 Damage)**: Traditional stat systems only store single scalars. Calculating "15~35 attack scaled by 30%" requires manually splitting formulas into Min and Max, doubling the code.
3. **Cross-Stat Scaling Loops**: "Every 10 Strength grants 2 Attack" or "5% of Max HP converts into Armor" can trigger infinite recursion and game-freezing crashes if calculated naively.
4. **Equipment Swap Lag & GC Allocation**: Using dictionaries to look up stats is slow, and swapping a full set of gear triggers dozens of recalculations and garbage collection spikes.
5. **In-Flight Projectiles Getting Modified**: A mage casts a fireball, but before it hits the target, an attack buff expires, causing the in-flight fireball's damage to drop unexpectedly.

`CwcStatSystem` is a **high-performance, modern, zero-garbage-collection (0-GC)** RPG stat calculation framework designed for Unity. It wraps complex combat formulas, range intervals, and cross-stat scaling into clean, intuitive APIs that work out of the box.

---

## Key Problems It Solves

### 1. Six Canonical RPG Multiplier Tiers (Automatic Layering)
Formulas follow canonical RPG math automatically without manual ordering:
- **FlatAdd (Base Add / Flat Damage)**: Adds directly to the base value first (e.g., Attack +10);
- **PercentAdd (Increased / Additive Percentage)**: All percentages sum together before scaling (e.g., +15% from gear and +20% from talents combine into +35%);
- **MoreMult (Independent Multiplier / PoE-Style More)**: Multiplies independently without diminishing returns (e.g., Berserk x1.2 and Critical Boost x1.5 multiply together);
- **PostFlatAdd (Post-Additive Flat)**: Added after all multipliers without being magnified again;
- **Override (Absolute Value Lock)**: Overrides the final stat directly (e.g., Frozen status locks Move Speed to 0, supporting Min-wins or Max-wins policies).

### 2. Native Interval Range Support (e.g., 15~35 Floating Damage)
- Natively supports floating intervals (`StatRange`).
- A weapon with base damage `[15, 35]` receiving a +50% multiplier scales both ends to `[22.5, 52.5]` automatically.

### 3. Cross-Stat Synergy Conversion Without Recursion Loops
- Supports complex conversions: "Every 1 Strength grants 0.5 Physical Damage" or "10% of Max HP converts to Armor".
- Evaluated via a high-speed, two-pass orthogonal linear scan, eliminating infinite loops and running in nanoseconds.

### 4. Zero-GC Performance & Lazy Dirty-Flag Baking
- Internal data is stored in flat arrays (`BakedStatSlot[]`) without dictionary hash lookup overhead.
- Swapping gear or adding buffs only marks a dirty flag. **Values are recalculated strictly on-demand when read**, keeping frame rates completely smooth.

### 5. Snapshot Isolation (Locking In-Flight Projectile Values)
- Export immutable, read-only value snapshots (`StatSnapshot`).
- Projectiles carry the snapshot upon firing, ensuring that buffs expiring or clearing on the character will not change in-flight damage.

### 6. Conditional Modifiers & Bitmask Filtering
- Supports condition-based modifiers (e.g., "+20% Critical Chance against Frozen Targets").
- Matched instantly using pure `ulong` bitmasks with zero performance overhead.

### 7. Strongly-Typed Code Generation (No Typos)
- Configure stats in the `StatConfigSO` asset (e.g., Health, Attack, Defense, MoveSpeed).
- Click **Generate Code** to automatically produce strongly-typed C# Enums and registries!
- Reference stats cleanly via `Stats.Attack` with full IDE autocomplete and compile-time safety.

---

## Installation

### Option A: Via Unity Package Manager (Recommended)
1. In the Unity Editor, open `Window` -> `Package Manager`.
2. Click the `+` button in the top-left corner -> Select **Add package from git URL...**.
3. Enter the repository URL:
   ```text
   https://github.com/CwcbbChao/CwcStatSystem.git
   ```
4. Click **Add** to install.

### Option B: Copy Files Directly
Clone or copy the `CwcStatSystem` directory directly into your project's `Assets/` or `Packages/` folder.

---

## Quick Start (3 Steps)

### Step 1: Configure Stats & Generate Code
1. Right-click in the Project window: `Create -> Cwcbb -> Stat System -> Stat Config` (e.g., `PlayerStatsConfig.asset`);
2. Add your game stats (Health, Attack, MoveSpeed, etc.);
3. Click the **`Generate Code`** button in the Inspector to generate the strongly-typed enum scripts.

---

### Step 2: Initialize StatContainer on Character
```csharp
using UnityEngine;
using Cwcbb.Tools.CwcStatSystem;

public class CharacterStatsComponent : MonoBehaviour
{
    private StatContainer _stats;

    private void Awake()
    {
        // 1. Initialize container (pass total number of stats)
        _stats = new StatContainer(numStats: 10);

        // 2. Subscribe to stat changes (e.g., update health UI)
        _stats.SubscribeToStatChanged(0, (statId, oldValue, newValue) =>
        {
            Debug.Log($"Stat changed: {oldValue} -> {newValue}");
        });
    }

    public StatContainer Stats => _stats;
}
```

---

### Step 3: Equip Gear, Add Buffs & Read Values!

#### 1. Equip Gear and Add Modifiers
```csharp
// Equip Greatsword: Flat base damage +15
_stats.AddModifier(new StatModifier(
    targetStatId: 0, // Attack
    value: 15f,
    modifyType: ModifierType.FlatAdd,
    source: swordItem
));

// Equip Ring of Fury: Attack +25%
_stats.AddModifier(new StatModifier(
    targetStatId: 0,
    value: 0.25f,
    modifyType: ModifierType.PercentAdd,
    source: ringItem
));

// Activate Bloodlust Buff: Independent multiplier x1.3 (More tier)
_stats.AddModifier(new StatModifier(
    targetStatId: 0,
    value: 0.30f,
    modifyType: ModifierType.MoreMult,
    source: bloodlustBuff
));
```

#### 2. Read Final Evaluated Values
```csharp
// Read scalar value (instant regular cache lookup, zero nanoseconds):
float finalAttack = _stats.GetStatValue(0);

// Read interval range (e.g., 20~45 floating damage):
StatRange attackRange = _stats.GetStatRange(0);
Debug.Log($"Attack range: {attackRange.Min} ~ {attackRange.Max}");
```

#### 3. Unequip Gear (Clean All Modifiers from Source)
```csharp
// Unequipping ring automatically removes all associated modifiers and updates stats:
_stats.RemoveModifiersFromSource(ringItem);
```

---

## Multiplier Formulas Reference

| Modifier Type | Meaning | Calculation Rule | Example Use Case |
| :--- | :--- | :--- | :--- |
| **`FlatAdd`** | **Flat Additive** | Added to base value: `(Base + Flat)` | Weapon Attack +15, Strength +5 |
| **`PercentAdd`** | **Additive Percentage** | Summed before scaling: `x (1 + Sum%)` | "+15% Attack" on gear, talent nodes |
| **`MoreMult`** | **Independent Multiplier** | Multiplies independently: `x (1 + More1) x (1 + More2)` | Berserk buff damage amplification, crit multipliers |
| **`DirectMult`** | **Direct Multiplier** | Multiplies directly: `x Value` | Global difficulty scalar, weakness debuffs |
| **`PostFlatAdd`** | **Post Flat Additive** | Added after multipliers: `+ PostFlat` | True flat damage, flat shield buffers |
| **`Override`** | **Value Lock** | Replaces all math directly | Frozen status (Move Speed = 0), invulnerability |

---

## License & Commercial Use

- Core source code is licensed under the [Cwc Tools Public License (Source-Available)](LICENSE):
  - **Game Projects (End Products)**: **Free to use in personal and commercial games with zero royalties (Royalty-Free)**.
  - **No Tool Resale**: You may not repackage or resell this software as a standalone asset pack, development tool, or plugin on marketplaces.
- See [Third-Party Notices.txt](Third-Party%20Notices.txt) for details.
