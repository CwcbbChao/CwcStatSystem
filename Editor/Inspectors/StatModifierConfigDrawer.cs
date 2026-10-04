using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cwcbb.Tools.CwcStatSystem.Editor
{
    /// <summary>
    /// StatModifierConfig 通用属性绘制器 (PropertyDrawer)。
    /// 插件原生自带，使项目中任意包含 [SerializeField] StatModifierConfig 的字段自动获得高质量 Inspector 交互：
    /// 1. 自动扫描项目中配置的 StatConfigSO 资产，将目标属性 ID 转换为可读属性下拉框展示；
    /// 2. 直观呈现属性联动/缩放 (Stat Scaling / Synergy) 开关与源属性下拉选择；
    /// 3. 支持 ModifierType 乘区选择与条件 Tag 掩码配置。
    /// </summary>
    [CustomPropertyDrawer(typeof(StatModifierConfig))]
    public class StatModifierConfigDrawer : PropertyDrawer
    {
        #region 常量与静态字段

        private const float Spacing = 2f;
        private const float HelpBoxHeight = 28f;
        private const string NoneTagText = "None (Unconditional)";

        private static List<StatOption> _cachedStatOptions;
        private static string[] _cachedOptionNames;
        private static List<TagOption> _cachedTagOptions;
        private static bool _eventSubscribed;

        private struct StatOption
        {
            public int Id;
            public string DisplayName;
            public bool IsValid;
        }

        private struct TagOption
        {
            public int BitIndex;
            public string Name;
        }

        #endregion

        #region Unity PropertyDrawer 覆写

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float lineAdvance = lineHeight + Spacing;

            if (!property.isExpanded)
            {
                return lineHeight;
            }

            EnsureStatOptionsCache();

            SerializedProperty targetProp = property.FindPropertyRelative("_targetStatId");
            SerializedProperty sourceProp = property.FindPropertyRelative("_sourceStatId");

            bool isScaled = sourceProp != null && sourceProp.intValue >= 0;
            bool hasTargetWarning = targetProp != null && !IsStatIdValid(targetProp.intValue);
            bool hasSelfScalingWarning = isScaled && targetProp != null && sourceProp.intValue == targetProp.intValue;

            // 基础行数: Header + TargetStat + ModifyType + ScalingToggle + TagMask
            int totalLines = 5;

            if (isScaled)
            {
                totalLines += 1; // SourceStat 行
            }

            float height = totalLines * lineAdvance + 6f;

            if (hasTargetWarning)
            {
                height += HelpBoxHeight + Spacing;
            }

            if (hasSelfScalingWarning)
            {
                height += HelpBoxHeight + Spacing;
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureStatOptionsCache();

            EditorGUI.BeginProperty(position, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float lineAdvance = lineHeight + Spacing;
            Rect currentRect = new Rect(position.x, position.y, position.width, lineHeight);

            // 1. 折叠头
            SerializedProperty targetProp = property.FindPropertyRelative("_targetStatId");
            SerializedProperty modifyTypeProp = property.FindPropertyRelative("_modifyType");
            SerializedProperty sourceProp = property.FindPropertyRelative("_sourceStatId");
            SerializedProperty tagMaskProp = property.FindPropertyRelative("_requiredTagMask");

            string headerSummary = GetHeaderSummary(targetProp, modifyTypeProp, sourceProp, tagMaskProp);
            property.isExpanded = EditorGUI.Foldout(currentRect, property.isExpanded, $"{label.text} ({headerSummary})", true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            currentRect.y += lineAdvance + 1f;

            // 2. 目标属性选择
            if (targetProp != null)
            {
                DrawStatSelector(currentRect, "Target Stat", targetProp, false);
                currentRect.y += lineAdvance;

                if (!IsStatIdValid(targetProp.intValue))
                {
                    Rect warningRect = new Rect(currentRect.x, currentRect.y, currentRect.width, HelpBoxHeight);
                    EditorGUI.HelpBox(warningRect, $"Stat ID [{targetProp.intValue}] is not defined in any active StatConfigSO.", MessageType.Warning);
                    currentRect.y += HelpBoxHeight + Spacing;
                }
            }

            // 3. 乘区类型选择
            if (modifyTypeProp != null)
            {
                EditorGUI.PropertyField(currentRect, modifyTypeProp, new GUIContent("Modify Type", "Calculation bucket (FlatAdd, PercentAdd, MoreMult, DirectMult, PostFlatAdd, Override)"));
                currentRect.y += lineAdvance;
            }

            // 4. 属性联动/缩放开关
            if (sourceProp != null)
            {
                bool isScaled = sourceProp.intValue >= 0;
                bool newIsScaled = EditorGUI.Toggle(
                    currentRect,
                    new GUIContent("Enable Stat Scaling", "Scale modifier value dynamically based on source stat native value (Synergy)"),
                    isScaled);

                currentRect.y += lineAdvance;

                if (newIsScaled)
                {
                    if (!isScaled)
                    {
                        sourceProp.intValue = GetDefaultSourceStatId(targetProp != null ? targetProp.intValue : 0);
                    }

                    EditorGUI.indentLevel++;
                    DrawStatSelector(currentRect, "Source Stat", sourceProp, true);
                    EditorGUI.indentLevel--;
                    currentRect.y += lineAdvance;

                    if (targetProp != null && sourceProp.intValue == targetProp.intValue)
                    {
                        Rect warningRect = new Rect(currentRect.x, currentRect.y, currentRect.width, HelpBoxHeight);
                        EditorGUI.HelpBox(warningRect, "Source stat cannot be the same as target stat.", MessageType.Error);
                        currentRect.y += HelpBoxHeight + Spacing;
                    }
                }
                else
                {
                    sourceProp.intValue = StatModifierConfig.NoSourceStat;
                }
            }

            // 5. 标签掩码选择器 (支持 64 位 Tag 多选菜单)
            if (tagMaskProp != null)
            {
                DrawTagMaskSelector(currentRect, tagMaskProp);
                currentRect.y += lineAdvance;
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        #endregion

        #region 私有方法

        private static void DrawStatSelector(Rect rect, string label, SerializedProperty idProp, bool isSource)
        {
            if (_cachedStatOptions == null || _cachedStatOptions.Count == 0)
            {
                EditorGUI.PropertyField(rect, idProp, new GUIContent(label));
                return;
            }

            int currentId = idProp.intValue;
            int selectedIndex = -1;

            for (int i = 0; i < _cachedStatOptions.Count; i++)
            {
                if (_cachedStatOptions[i].Id == currentId)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                // 若当前 ID 在现有配置中不存在，显示并允许使用者通过下拉切换
                List<string> tempNames = new List<string>(_cachedOptionNames);
                tempNames.Insert(0, $"[Unknown ID: {currentId}]");

                int choice = EditorGUI.Popup(rect, label, 0, tempNames.ToArray());
                if (choice > 0)
                {
                    idProp.intValue = _cachedStatOptions[choice - 1].Id;
                }
                return;
            }

            int newIndex = EditorGUI.Popup(rect, label, selectedIndex, _cachedOptionNames);
            if (newIndex != selectedIndex && newIndex >= 0 && newIndex < _cachedStatOptions.Count)
            {
                idProp.intValue = _cachedStatOptions[newIndex].Id;
            }
        }

        private static void DrawTagMaskSelector(Rect rect, SerializedProperty tagMaskProp)
        {
            ulong currentMask = unchecked((ulong)tagMaskProp.longValue);
            string summaryText = FormatTagMaskSummary(currentMask);

            Rect labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
            Rect popupRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y, rect.width - EditorGUIUtility.labelWidth, rect.height);

            EditorGUI.PrefixLabel(labelRect, new GUIContent("Required Tags", "Conditional tags mask (64-bit space)"));

            if (GUI.Button(popupRect, summaryText, EditorStyles.popup))
            {
                ShowTagMaskMenu(popupRect, tagMaskProp, currentMask);
            }
        }

        private static void ShowTagMaskMenu(Rect buttonRect, SerializedProperty tagMaskProp, ulong currentMask)
        {
            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("Nothing (Unconditional)"), currentMask == 0UL, () =>
            {
                ApplyTagMaskChange(tagMaskProp, 0UL);
            });

            if (_cachedTagOptions != null && _cachedTagOptions.Count > 0)
            {
                ulong allMask = 0UL;
                for (int i = 0; i < _cachedTagOptions.Count; i++)
                {
                    allMask |= (1UL << _cachedTagOptions[i].BitIndex);
                }

                menu.AddItem(new GUIContent("Everything"), (currentMask & allMask) == allMask, () =>
                {
                    ApplyTagMaskChange(tagMaskProp, allMask);
                });

                menu.AddSeparator(string.Empty);

                for (int i = 0; i < _cachedTagOptions.Count; i++)
                {
                    var opt = _cachedTagOptions[i];
                    ulong bit = 1UL << opt.BitIndex;
                    bool isChecked = (currentMask & bit) != 0UL;
                    int capturedIndex = opt.BitIndex;

                    menu.AddItem(new GUIContent($"[{opt.BitIndex}] {opt.Name}"), isChecked, () =>
                    {
                        ulong updatedMask = currentMask ^ (1UL << capturedIndex);
                        ApplyTagMaskChange(tagMaskProp, updatedMask);
                    });
                }
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("No tags defined in StatConfigSO"));
            }

            menu.DropDown(buttonRect);
        }

        private static void ApplyTagMaskChange(SerializedProperty tagMaskProp, ulong newMask)
        {
            if (tagMaskProp == null || tagMaskProp.serializedObject == null)
            {
                return;
            }

            tagMaskProp.serializedObject.Update();
            tagMaskProp.longValue = unchecked((long)newMask);
            tagMaskProp.serializedObject.ApplyModifiedProperties();
        }

        private static string FormatTagMaskSummary(ulong mask)
        {
            if (mask == 0UL)
            {
                return NoneTagText;
            }

            if (_cachedTagOptions == null || _cachedTagOptions.Count == 0)
            {
                return $"Raw: 0x{mask:X}";
            }

            List<string> activeNames = new List<string>();
            ulong matchedMask = 0UL;

            for (int i = 0; i < _cachedTagOptions.Count; i++)
            {
                var opt = _cachedTagOptions[i];
                ulong bit = 1UL << opt.BitIndex;
                if ((mask & bit) != 0UL)
                {
                    activeNames.Add(opt.Name);
                    matchedMask |= bit;
                }
            }

            bool hasUnknownBits = (mask & ~matchedMask) != 0UL;

            if (activeNames.Count == 0)
            {
                return $"Custom (0x{mask:X})";
            }

            if (activeNames.Count <= 2 && !hasUnknownBits)
            {
                return string.Join(", ", activeNames);
            }

            int extraCount = activeNames.Count - 2;
            string baseSummary = $"{activeNames[0]}, {activeNames[1]} (+{extraCount} more)";
            return hasUnknownBits ? $"{baseSummary} (+Custom)" : baseSummary;
        }

        private static string GetHeaderSummary(
            SerializedProperty targetProp,
            SerializedProperty modifyTypeProp,
            SerializedProperty sourceProp,
            SerializedProperty tagMaskProp)
        {
            string targetName = "Unassigned";
            if (targetProp != null)
            {
                targetName = FindStatName(targetProp.intValue);
            }

            string modTypeName = modifyTypeProp != null ? ((ModifierType)modifyTypeProp.intValue).ToString() : "Mod";

            string scalingSummary = string.Empty;
            if (sourceProp != null && sourceProp.intValue >= 0)
            {
                string sourceName = FindStatName(sourceProp.intValue);
                scalingSummary = $" <- {sourceName}";
            }

            string tagSummary = string.Empty;
            if (tagMaskProp != null)
            {
                ulong mask = unchecked((ulong)tagMaskProp.longValue);
                if (mask != 0UL)
                {
                    tagSummary = $" [{FormatTagMaskSummary(mask)}]";
                }
            }

            return $"{targetName} ({modTypeName}{scalingSummary}){tagSummary}";
        }

        private static string FindStatName(int statId)
        {
            if (_cachedStatOptions != null)
            {
                for (int i = 0; i < _cachedStatOptions.Count; i++)
                {
                    if (_cachedStatOptions[i].Id == statId)
                    {
                        return _cachedStatOptions[i].DisplayName;
                    }
                }
            }

            return $"ID:{statId}";
        }

        private static bool IsStatIdValid(int statId)
        {
            if (_cachedStatOptions == null || _cachedStatOptions.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < _cachedStatOptions.Count; i++)
            {
                if (_cachedStatOptions[i].Id == statId)
                {
                    return _cachedStatOptions[i].IsValid;
                }
            }

            return false;
        }

        private static int GetDefaultSourceStatId(int currentTargetId)
        {
            if (_cachedStatOptions != null)
            {
                for (int i = 0; i < _cachedStatOptions.Count; i++)
                {
                    int id = _cachedStatOptions[i].Id;
                    if (id != currentTargetId && _cachedStatOptions[i].IsValid)
                    {
                        return id;
                    }
                }
            }

            return 0;
        }

        private static void EnsureStatOptionsCache()
        {
            if (!_eventSubscribed)
            {
                _eventSubscribed = true;
                EditorApplication.projectChanged += InvalidateCache;
                AssemblyReloadEvents.afterAssemblyReload += InvalidateCache;
            }

            if (_cachedStatOptions != null)
            {
                return;
            }

            var options = new List<StatOption>();
            var tagOptions = new List<TagOption>();

            // 1. 优先扫描项目中的所有 StatConfigSO
            string[] configGuids = AssetDatabase.FindAssets("t:StatConfigSO");
            bool hasMultipleConfigs = configGuids != null && configGuids.Length > 1;

            if (configGuids != null && configGuids.Length > 0)
            {
                for (int c = 0; c < configGuids.Length; c++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(configGuids[c]);
                    var config = AssetDatabase.LoadAssetAtPath<StatConfigSO>(path);
                    if (config == null || config.StatDefinitions == null)
                    {
                        continue;
                    }

                    // 属性项：ID 严格对应列表下标 i (与 StatCodeGenerator 保持完全一致，绝无偏移)
                    int count = config.StatDefinitions.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var def = config.StatDefinitions[i];
                        string statName = def != null ? def.StatKey : $"Missing (Index {i})";
                        string prefix = hasMultipleConfigs ? $"[{config.ConfigName}] " : string.Empty;

                        options.Add(new StatOption
                        {
                            Id = i,
                            DisplayName = $"{prefix}[{i}] {statName}",
                            IsValid = def != null
                        });
                    }

                    // 收集标签定义 (单周期最多 64 位)
                    var tags = config.TagNames;
                    if (tags != null)
                    {
                        int tagCount = Math.Min(64, tags.Count);
                        for (int t = 0; t < tagCount; t++)
                        {
                            string tagName = tags[t];
                            if (!string.IsNullOrWhiteSpace(tagName))
                            {
                                tagOptions.Add(new TagOption
                                {
                                    BitIndex = t,
                                    Name = tagName.Trim()
                                });
                            }
                        }
                    }
                }
            }

            // 2. 兜底策略：若未找到任何 StatConfigSO 资产，则扫描独立的 StatDefinitionSO
            if (options.Count == 0)
            {
                string[] defGuids = AssetDatabase.FindAssets("t:StatDefinitionSO");
                if (defGuids != null && defGuids.Length > 0)
                {
                    for (int i = 0; i < defGuids.Length; i++)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(defGuids[i]);
                        var def = AssetDatabase.LoadAssetAtPath<StatDefinitionSO>(path);
                        if (def != null)
                        {
                            options.Add(new StatOption
                            {
                                Id = i,
                                DisplayName = $"[{i}] {def.StatKey}",
                                IsValid = true
                            });
                        }
                    }
                }
            }

            // 3. 若依然为空，添加默认占位选项
            if (options.Count == 0)
            {
                options.Add(new StatOption
                {
                    Id = 0,
                    DisplayName = "[0] (No Stat Definitions Found)",
                    IsValid = false
                });
            }

            _cachedStatOptions = options;
            _cachedOptionNames = new string[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                _cachedOptionNames[i] = options[i].DisplayName;
            }

            _cachedTagOptions = tagOptions;
        }

        private static void InvalidateCache()
        {
            _cachedStatOptions = null;
            _cachedOptionNames = null;
            _cachedTagOptions = null;
        }

        #endregion
    }
}
