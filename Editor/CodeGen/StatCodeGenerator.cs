using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using Cwcbb.Tools.CwcStatSystem;
using UnityEditor;
using UnityEngine;

namespace Cwcbb.Tools.CwcStatSystem.Editor
{
    /// <summary>
    /// StatConfigSO 与已生成代码的差异校验结果报告
    /// </summary>
    public class StatConfigDiffReport
    {
        public bool HasDataErrors;
        public readonly List<string> ErrorMessages = new List<string>();
        public readonly List<int> NullDefinitionIndices = new List<int>();

        public bool CodeFilesExist;
        public bool IsUpToDate;
        public string TargetDirectory = string.Empty;

        public readonly List<string> AddedStats = new List<string>();
        public readonly List<string> RemovedStats = new List<string>();
        public readonly List<string> ModifiedOrReorderedStats = new List<string>();
        public readonly List<string> AddedTags = new List<string>();
        public readonly List<string> RemovedTags = new List<string>();

        public bool HasDifferences => !IsUpToDate;
    }

    /// <summary>
    /// 强类型代码生成器 (为每个 StatConfigSO 独立生成专属的 Enum, TagMask 与 Registry)
    /// </summary>
    public static class StatCodeGenerator
    {
        public static bool Generate(StatConfigSO config)
        {
            if (config == null)
            {
                Debug.LogError("[CwcStatSystem] StatConfigSO 不能为空！");
                return false;
            }

            var report = CheckDiff(config);
            if (report.HasDataErrors)
            {
                var errorMsg = string.Join("\n", report.ErrorMessages);
                Debug.LogError($"[CwcStatSystem] 生成失败，配置数据存在错误:\n{errorMsg}");
                EditorUtility.DisplayDialog("CwcStatSystem 代码生成错误", $"配置存在错误，已阻止生成:\n\n{errorMsg}", "确定");
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("[CwcStatSystem] 无法获取 StatConfigSO 资产路径，请确保资产已保存！");
                return false;
            }

            var targetDir = ResolveTargetDirectory(config, assetPath);

            // 1. 移动自清理机制：若发现资产移动导致目标路径改变，安全删除旧目录残留文件
            if (!string.IsNullOrEmpty(config.LastGeneratedDirectory) && config.LastGeneratedDirectory != targetDir)
            {
                if (AssetDatabase.IsValidFolder(config.LastGeneratedDirectory))
                {
                    AssetDatabase.DeleteAsset(config.LastGeneratedDirectory);
                    Debug.Log($"[CwcStatSystem] 检测到代码输出路径变动，已自动清理旧代码目录: {config.LastGeneratedDirectory}");
                }
            }

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            config.LastGeneratedDirectory = targetDir;
            EditorUtility.SetDirty(config);

            var configName = CleanIdentifier(config.ConfigName);
            if (string.IsNullOrEmpty(configName))
            {
                configName = "Default";
            }

            var ns = string.IsNullOrEmpty(config.CodeNamespace) ? "Cwcbb.Tools.CwcStatSystem.Generated" : config.CodeNamespace;

            // 2. 生成 StatType 枚举
            var statTypePath = $"{targetDir}/{configName}StatType.cs";
            File.WriteAllText(statTypePath, GenerateStatTypeEnum(config, configName, ns), Encoding.UTF8);

            // 3. 生成 Tag 枚举
            var tagPath = $"{targetDir}/{configName}Tag.cs";
            File.WriteAllText(tagPath, GenerateTagEnum(config, configName, ns), Encoding.UTF8);

            // 4. 生成 TagMask 单周期位掩码结构体
            var tagMaskPath = $"{targetDir}/{configName}TagMask.cs";
            File.WriteAllText(tagMaskPath, GenerateTagMaskStruct(configName, ns), Encoding.UTF8);

            // 5. 生成 Registry 静态映射与扩展方法
            var registryPath = $"{targetDir}/{configName}StatRegistry.cs";
            File.WriteAllText(registryPath, GenerateRegistryClass(config, configName, ns, assetPath), Encoding.UTF8);

            config.LastGeneratedDirectory = targetDir;
            config.LastGeneratedHash = ComputeConfigHash(config);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();
            Debug.Log($"[CwcStatSystem] 成功为 [{configName}] 生成独立强类型代码于: {targetDir}");
            return true;
        }

        /// <summary>
        /// 校验 StatConfigSO 与磁盘上已生成代码的差异状态，并进行数据合法性诊断
        /// </summary>
        public static StatConfigDiffReport CheckDiff(StatConfigSO config)
        {
            var report = new StatConfigDiffReport();
            if (config == null)
            {
                report.HasDataErrors = true;
                report.ErrorMessages.Add("配置资产对象为 null！");
                return report;
            }

            var assetPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(assetPath))
            {
                report.HasDataErrors = true;
                report.ErrorMessages.Add("资产尚未持久化到工程，请先保存资产！");
                return report;
            }
            
            var targetDir = ResolveTargetDirectory(config, assetPath);
            report.TargetDirectory = targetDir;

            var configName = CleanIdentifier(config.ConfigName);
            if (string.IsNullOrEmpty(configName))
            {
                configName = "Default";
            }

            var ns = string.IsNullOrEmpty(config.CodeNamespace) ? "Cwcbb.Tools.CwcStatSystem.Generated" : config.CodeNamespace;

            // 1. 数据完整性检查
            var statCount = config.StatDefinitions != null ? config.StatDefinitions.Count : 0;
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < statCount; i++)
            {
                var def = config.StatDefinitions[i];
                if (def == null)
                {
                    report.HasDataErrors = true;
                    report.NullDefinitionIndices.Add(i);
                    report.ErrorMessages.Add($"属性列表索引 [{i}] 引用为空 (None)！");
                    continue;
                }

                var key = CleanIdentifier(def.StatKey);
                if (string.IsNullOrEmpty(key))
                {
                    report.HasDataErrors = true;
                    report.ErrorMessages.Add($"属性列表索引 [{i}] ({def.name}) 的 StatKey 为空或包含非法字符！");
                }
                else if (!seenKeys.Add(key))
                {
                    report.HasDataErrors = true;
                    report.ErrorMessages.Add($"存在重复的属性键名: [{key}] (在索引 [{i}] 处重复)！");
                }
            }

            var tags = config.TagNames;
            var tagCount = tags != null ? tags.Count : 0;
            if (tagCount > 64)
            {
                report.HasDataErrors = true;
                report.ErrorMessages.Add($"标签数量 ({tagCount}) 超过了 64 个上限，单周期 64 位掩码无法容纳！");
            }

            var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < tagCount; i++)
            {
                var tag = tags[i];
                if (string.IsNullOrWhiteSpace(tag))
                {
                    report.HasDataErrors = true;
                    report.ErrorMessages.Add($"标签列表索引 [{i}] 处的值为空！");
                    continue;
                }
                var cleanedTag = CleanIdentifier(tag);
                if (!seenTags.Add(cleanedTag))
                {
                    report.HasDataErrors = true;
                    report.ErrorMessages.Add($"存在重复的标签名称: [{cleanedTag}]！");
                }
            }

            // 2. 检查生成文件是否存在
            var statTypePath = $"{targetDir}/{configName}StatType.cs";
            var tagPath = $"{targetDir}/{configName}Tag.cs";
            var tagMaskPath = $"{targetDir}/{configName}TagMask.cs";
            var registryPath = $"{targetDir}/{configName}StatRegistry.cs";

            if (!File.Exists(statTypePath) || !File.Exists(tagPath) || !File.Exists(tagMaskPath) || !File.Exists(registryPath))
            {
                report.CodeFilesExist = false;
                report.IsUpToDate = false;
                return report;
            }

            report.CodeFilesExist = true;

            // 3. 基于持久化特征哈希进行优先快速判定
            var currentHash = ComputeConfigHash(config);
            if (!string.IsNullOrEmpty(config.LastGeneratedHash) && config.LastGeneratedHash == currentHash)
            {
                report.IsUpToDate = !report.HasDataErrors;
                return report;
            }

            // 4. 解析磁盘上的现有 Enum 定义做差异比对 (供 UI 显示变动明细)
            var existingStats = ParseEnumMembers(statTypePath);
            var existingTags = ParseEnumMembers(tagPath);

            var currentStatKeys = new List<string>();
            for (var i = 0; i < statCount; i++)
            {
                var def = config.StatDefinitions[i];
                var key = def != null ? CleanIdentifier(def.StatKey) : $"Stat_{i}";
                if (string.IsNullOrEmpty(key)) key = $"Stat_{i}";
                currentStatKeys.Add(key);

                if (!existingStats.TryGetValue(key, out var existingIndex))
                {
                    report.AddedStats.Add(key);
                }
                else if (existingIndex != i)
                {
                    report.ModifiedOrReorderedStats.Add($"{key} (原索引: {existingIndex} -> 现索引: {i})");
                }
            }

            foreach (var kvp in existingStats)
            {
                if (!currentStatKeys.Contains(kvp.Key))
                {
                    report.RemovedStats.Add(kvp.Key);
                }
            }

            var currentTagKeys = new List<string>();
            for (var i = 0; i < Math.Min(64, tagCount); i++)
            {
                var tag = CleanIdentifier(tags[i]);
                if (string.IsNullOrEmpty(tag)) continue;
                currentTagKeys.Add(tag);

                if (!existingTags.ContainsKey(tag))
                {
                    report.AddedTags.Add(tag);
                }
            }

            foreach (var kvp in existingTags)
            {
                if (!currentTagKeys.Contains(kvp.Key))
                {
                    report.RemovedTags.Add(kvp.Key);
                }
            }

            // 5. 若 LastGeneratedHash 尚未持久化（兼容历史生成文件），做一次精确文本一致性比对
            if (string.IsNullOrEmpty(config.LastGeneratedHash))
            {
                var expectedStatType = NormalizeLineEndings(GenerateStatTypeEnum(config, configName, ns));
                var actualStatType = NormalizeLineEndings(File.ReadAllText(statTypePath));

                var expectedTag = NormalizeLineEndings(GenerateTagEnum(config, configName, ns));
                var actualTag = NormalizeLineEndings(File.ReadAllText(tagPath));

                var expectedRegistry = NormalizeLineEndings(GenerateRegistryClass(config, configName, ns, assetPath));
                var actualRegistry = NormalizeLineEndings(File.ReadAllText(registryPath));

                var isIdentical = expectedStatType == actualStatType &&
                                  expectedTag == actualTag &&
                                  expectedRegistry == actualRegistry;

                if (isIdentical && !report.HasDataErrors)
                {
                    config.LastGeneratedHash = currentHash;
                    EditorUtility.SetDirty(config);
                    report.IsUpToDate = true;
                    return report;
                }
            }

            report.IsUpToDate = false;
            return report;
        }

        private static Dictionary<string, int> ParseEnumMembers(string filePath)
        {
            var dict = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!File.Exists(filePath)) return dict;

            var lines = File.ReadAllLines(filePath);
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("//") || trimmed.StartsWith("/*"))
                {
                    continue;
                }

                var commentIdx = trimmed.IndexOf("//", StringComparison.Ordinal);
                if (commentIdx >= 0)
                {
                    trimmed = trimmed.Substring(0, commentIdx).Trim();
                }

                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex > 0)
                {
                    var name = trimmed.Substring(0, eqIndex).Trim();
                    var valPart = trimmed.Substring(eqIndex + 1).Trim().TrimEnd(',', ';');
                    if (int.TryParse(valPart, out var val))
                    {
                        dict[name] = val;
                    }
                }
            }

            return dict;
        }

        private static string NormalizeLineEndings(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r\n", "\n").Trim();
        }

        /// <summary>
        /// 解析代码输出目标路径：若为空或为相对路径，则以当前配置资产所在目录为基准进行相对解析
        /// </summary>
        public static string ResolveTargetDirectory(StatConfigSO config, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return string.Empty;

            var currentDir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var customDir = config?.CustomOutputDirectory?.Trim().Replace('\\', '/');

            if (string.IsNullOrEmpty(customDir))
            {
                return $"{currentDir}/Generated";
            }

            if (customDir.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return customDir;
            }

            while (customDir.StartsWith("./"))
            {
                customDir = customDir.Substring(2);
            }

            return $"{currentDir}/{customDir}".Replace("//", "/");
        }

        /// <summary>
        /// 计算当前配置核心数据的特征哈希，用于常态判断代码是否最新
        /// </summary>
        public static string ComputeConfigHash(StatConfigSO config)
        {
            if (config == null) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("generator_v2|");
            sb.Append(config.ConfigName ?? "").Append('|');
            sb.Append(config.CodeNamespace ?? "").Append('|');

            var statDefs = config.StatDefinitions;
            var statCount = statDefs != null ? statDefs.Count : 0;
            sb.Append("stats:").Append(statCount).Append(';');
            for (var i = 0; i < statCount; i++)
            {
                var def = statDefs[i];
                sb.Append(i).Append(':').Append(def != null ? def.StatKey : "").Append(';');
            }

            var tags = config.TagNames;
            var tagCount = tags != null ? tags.Count : 0;
            sb.Append("tags:").Append(tagCount).Append(';');
            for (var i = 0; i < tagCount; i++)
            {
                sb.Append(i).Append(':').Append(tags[i] ?? "").Append(';');
            }

            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var hashBytes = md5.ComputeHash(bytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        internal static string GenerateStatTypeEnum(StatConfigSO config, string configName, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// 此代码由 CwcStatSystem 独立代码生成器自动生成，请勿手动修改。");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// [{configName}] 系统的强类型属性枚举");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public enum {configName}StatType : int");
            sb.AppendLine("    {");

            var count = config.StatDefinitions.Count;
            for (var i = 0; i < count; i++)
            {
                var def = config.StatDefinitions[i];
                var key = def != null ? CleanIdentifier(def.StatKey) : $"Stat_{i}";
                if (string.IsNullOrEmpty(key))
                {
                    key = $"Stat_{i}";
                }

                sb.AppendLine($"        {key} = {i},");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        internal static string GenerateTagEnum(StatConfigSO config, string configName, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// 此代码由 CwcStatSystem 独立代码生成器自动生成，请勿手动修改。");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// [{configName}] 系统的强类型标签位枚举 (64位空间)");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public enum {configName}Tag : byte");
            sb.AppendLine("    {");

            var tags = config.TagNames;
            var tagCount = tags != null ? tags.Count : 0;
            if (tagCount > 64)
            {
                Debug.LogWarning($"[CwcStatSystem] 配置 [{configName}] 中的标签数量超过了 64 个上限，超出部分将被忽略！");
                tagCount = 64;
            }

            for (var i = 0; i < tagCount; i++)
            {
                var tag = CleanIdentifier(tags[i]);
                if (!string.IsNullOrEmpty(tag))
                {
                    sb.AppendLine($"        {tag} = {i},");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        internal static string GenerateTagMaskStruct(string configName, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// 此代码由 CwcStatSystem 独立代码生成器自动生成，请勿手动修改。");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// [{configName}] 系统的单周期 64 位极速位掩码");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public readonly struct {configName}TagMask : IEquatable<{configName}TagMask>");
            sb.AppendLine("    {");
            sb.AppendLine("        public readonly ulong Value;");
            sb.AppendLine();
            sb.AppendLine("        public bool IsEmpty => Value == 0UL;");
            sb.AppendLine();
            sb.AppendLine($"        public {configName}TagMask(ulong value)");
            sb.AppendLine("        {");
            sb.AppendLine("            Value = value;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public {configName}TagMask({configName}Tag tag)");
            sb.AppendLine("        {");
            sb.AppendLine("            Value = 1UL << (int)tag;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public bool Matches(in {configName}TagMask contextMask)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (Value == 0UL) return true;");
            sb.AppendLine("            return (contextMask.Value & Value) == Value;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public bool HasTag({configName}Tag tag)");
            sb.AppendLine("        {");
            sb.AppendLine("            return (Value & (1UL << (int)tag)) != 0UL;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static {configName}TagMask operator |({configName}TagMask a, {configName}TagMask b) => new {configName}TagMask(a.Value | b.Value);");
            sb.AppendLine($"        public static {configName}TagMask operator |({configName}TagMask a, {configName}Tag b) => new {configName}TagMask(a.Value | (1UL << (int)b));");
            sb.AppendLine($"        public static {configName}TagMask operator &({configName}TagMask a, {configName}TagMask b) => new {configName}TagMask(a.Value & b.Value);");
            sb.AppendLine($"        public static {configName}TagMask operator ~({configName}TagMask a) => new {configName}TagMask(~a.Value);");
            sb.AppendLine($"        public static bool operator ==({configName}TagMask a, {configName}TagMask b) => a.Value == b.Value;");
            sb.AppendLine($"        public static bool operator !=({configName}TagMask a, {configName}TagMask b) => a.Value != b.Value;");
            sb.AppendLine();
            sb.AppendLine($"        public static implicit operator {configName}TagMask({configName}Tag tag) => new {configName}TagMask(tag);");
            sb.AppendLine($"        public static implicit operator ulong({configName}TagMask mask) => mask.Value;");
            sb.AppendLine($"        public static explicit operator {configName}TagMask(ulong value) => new {configName}TagMask(value);");
            sb.AppendLine();
            sb.AppendLine($"        public bool Equals({configName}TagMask other) => Value == other.Value;");
            sb.AppendLine("        public override bool Equals(object obj) => obj is " + configName + "TagMask other && Equals(other);");
            sb.AppendLine("        public override int GetHashCode() => Value.GetHashCode();");
            sb.AppendLine("        public override string ToString() => $\"TagMask({Convert.ToString((long)Value, 2).PadLeft(64, '0')})\";");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        internal static string GenerateRegistryClass(StatConfigSO config, string configName, string ns, string assetPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// 此代码由 CwcStatSystem 独立代码生成器自动生成，请勿手动修改。");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using Cwcbb.Tools.CwcStatSystem;");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// [{configName}] 系统的强类型注册表与扩展方法");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static class {configName}StatRegistry");
            sb.AppendLine("    {");
            sb.AppendLine($"        public const int StatCount = {config.StatDefinitions.Count};");
            sb.AppendLine($"        public const int TagCount = {Math.Min(64, config.TagNames.Count)};");
            sb.AppendLine($"        public const string ConfigAssetPath = \"{assetPath}\";");
            sb.AppendLine();
            sb.AppendLine($"        private static StatConfigSO _cachedConfig;");
            sb.AppendLine($"        public static StatConfigSO Config");
            sb.AppendLine("        {");
            sb.AppendLine("            get");
            sb.AppendLine("            {");
            sb.AppendLine("                if (_cachedConfig == null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    _cachedConfig = Resources.Load<StatConfigSO>(\"" + config.name + "\");");
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("                    if (_cachedConfig == null)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        _cachedConfig = AssetDatabase.LoadAssetAtPath<StatConfigSO>(ConfigAssetPath);");
            sb.AppendLine("                        if (_cachedConfig == null)");
            sb.AppendLine("                        {");
            sb.AppendLine("                            string[] guids = AssetDatabase.FindAssets(\"" + config.name + " t:StatConfigSO\");");
            sb.AppendLine("                            if (guids != null && guids.Length > 0)");
            sb.AppendLine("                            {");
            sb.AppendLine("                                string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);");
            sb.AppendLine("                                _cachedConfig = AssetDatabase.LoadAssetAtPath<StatConfigSO>(foundPath);");
            sb.AppendLine("                            }");
            sb.AppendLine("                        }");
            sb.AppendLine("                    }");
            sb.AppendLine("#endif");
            sb.AppendLine("                }");
            sb.AppendLine("                return _cachedConfig;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static StatDefinitionSO GetDefinition({configName}StatType statType)");
            sb.AppendLine("        {");
            sb.AppendLine("            var cfg = Config;");
            sb.AppendLine("            if (cfg != null && (int)statType >= 0 && (int)statType < cfg.StatDefinitions.Count)");
            sb.AppendLine("            {");
            sb.AppendLine("                return cfg.StatDefinitions[(int)statType];");
            sb.AppendLine("            }");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static StatContainer CreateContainer()");
            sb.AppendLine("        {");
            sb.AppendLine("            var cfg = Config;");
            sb.AppendLine($"            var container = new StatContainer(StatCount, id => cfg != null ? cfg.GetDefinitionData(id) : StatDefinitionData.Default);");
            sb.AppendLine("            return container;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static float GetValue(this StatContainer container, {configName}StatType stat)");
            sb.AppendLine("        {");
            sb.AppendLine("            return container.GetValue((int)stat);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static float GetValue(this StatContainer container, {configName}StatType stat, {configName}TagMask contextTags)");
            sb.AppendLine("        {");
            sb.AppendLine("            return container.GetValue((int)stat, contextTags.Value);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static StatRange GetRange(this StatContainer container, {configName}StatType stat)");
            sb.AppendLine("        {");
            sb.AppendLine("            return container.GetRange((int)stat);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static StatRange GetRange(this StatContainer container, {configName}StatType stat, {configName}TagMask contextTags)");
            sb.AppendLine("        {");
            sb.AppendLine("            return container.GetRange((int)stat, contextTags.Value);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static float RollValue(this StatContainer container, {configName}StatType stat)");
            sb.AppendLine("        {");
            sb.AppendLine("            return container.RollValue((int)stat);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static float RollValue(this StatContainer container, {configName}StatType stat, {configName}TagMask contextTags)");
            sb.AppendLine("        {");
            sb.AppendLine("            return container.RollValue((int)stat, contextTags.Value);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static float GetValue(this StatSnapshot snapshot, {configName}StatType stat)");
            sb.AppendLine("        {");
            sb.AppendLine("            return snapshot.GetValue((int)stat);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static float GetValue(this StatSnapshot snapshot, {configName}StatType stat, {configName}TagMask contextTags)");
            sb.AppendLine("        {");
            sb.AppendLine("            return snapshot.GetValue((int)stat, contextTags.Value);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static StatRange GetRange(this StatSnapshot snapshot, {configName}StatType stat)");
            sb.AppendLine("        {");
            sb.AppendLine("            return snapshot.GetRange((int)stat);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static StatRange GetRange(this StatSnapshot snapshot, {configName}StatType stat, {configName}TagMask contextTags)");
            sb.AppendLine("        {");
            sb.AppendLine("            return snapshot.GetRange((int)stat, contextTags.Value);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static float RollValue(this StatSnapshot snapshot, {configName}StatType stat)");
            sb.AppendLine("        {");
            sb.AppendLine("            return snapshot.RollValue((int)stat);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static float RollValue(this StatSnapshot snapshot, {configName}StatType stat, {configName}TagMask contextTags)");
            sb.AppendLine("        {");
            sb.AppendLine("            return snapshot.RollValue((int)stat, contextTags.Value);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static StatModifier CreateModifier({configName}StatType stat, ModifierType type, float value, {configName}TagMask requiredTags = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            return new StatModifier((int)stat, type, value, requiredTags.Value);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static StatModifier CreateModifier({configName}StatType stat, ModifierType type, float min, float max, {configName}TagMask requiredTags = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            return new StatModifier((int)stat, type, min, max, requiredTags.Value);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static StatModifier CreateScaledModifier({configName}StatType targetStat, {configName}StatType sourceStat, ModifierType type, float ratio, {configName}TagMask requiredTags = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            return new StatModifier((int)targetStat, (int)sourceStat, type, ratio, requiredTags.Value);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static IDisposable Subscribe(this StatContainer container, {configName}StatType stat, StatChangedHandler handler, bool triggerImmediately = false)");
            sb.AppendLine("        {");
            sb.AppendLine("            return container.Subscribe((int)stat, handler, triggerImmediately);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static void Unsubscribe(this StatContainer container, {configName}StatType stat, StatChangedHandler handler)");
            sb.AppendLine("        {");
            sb.AppendLine("            container.Unsubscribe((int)stat, handler);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        internal static string CleanIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var nextUpper = true;

            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    if (sb.Length == 0 && char.IsDigit(c))
                    {
                        sb.Append('_');
                    }

                    sb.Append(nextUpper ? char.ToUpperInvariant(c) : c);
                    nextUpper = false;
                }
                else
                {
                    nextUpper = true;
                }
            }

            return sb.ToString();
        }
    }
}
