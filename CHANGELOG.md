# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-10-04

### Added
- **Multi-Tier Multiplier Formula Architecture**: Six canonical RPG modifier stages (`FlatAdd`, `PercentAdd`, `MoreMult`, `DirectMult`, `PostFlatAdd`, `Override`) supporting complex combat calculations.
- **Native Interval Range Algebra (`StatRange`)**: Seamless calculation for floating damage ranges (e.g. 15~35 Attack) alongside scalar values without duplicate formulas.
- **Dual-Channel Orthogonal Two-Pass Synergy**: High-speed linear scan resolving cross-stat scaling (e.g. Strength converting to Physical Damage, Max HP scaling Armor) with zero circular dependency loops.
- **Lazy Dirty-Flag Baking (`StatContainer`)**: Marks slots dirty on modifier changes and evaluates strictly on-demand, preventing recalculation spikes during mass equipment swaps.
- **Snapshot Isolation (`StatSnapshot`)**: Export immutable, read-only value snapshots ensuring projectile attributes remain isolated from in-flight character stat changes.
- **Context Tag Mask Filtering**: High-performance `ulong` bitmask matching for conditional modifiers (e.g. "+30% Damage against Bleeding Targets").
- **Strongly-Typed Code Generator (`StatCodeGenerator`)**: Editor tool generating strongly-typed C# Enums, TagMasks, and slot registries directly from `StatConfigSO` assets, eliminating string dictionary lookups.
- **Zero-GC Flat Array Slots (`BakedStatSlot[]`)**: Pure array indexing achieving nanosecond cache-friendly evaluations without heap allocations.
- **Comprehensive Test Suite**: Automated editor tests covering mathematical formulas, dual-channel synergy, snapshot isolation, subscription events, and lazy dirty flags.
