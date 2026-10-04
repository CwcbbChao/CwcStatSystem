using System.IO;
using Cwcbb.Tools.CwcStatSystem;
using UnityEditor;
using UnityEngine;

namespace Cwcbb.Tools.CwcStatSystem.Editor
{
    /// <summary>
    /// StatConfigSO 自定义 Inspector 面板 (支持所有派生类如 CombatStatConfigSO)
    /// 保留原生常规编辑器界面，同时集成代码生成、差异校验与快捷工具。
    /// </summary>
    [CustomEditor(typeof(StatConfigSO), true)]
    public class StatConfigSOEditor : UnityEditor.Editor
    {
        #region 私有字段

        private bool _showDiffFoldout = true;
        private StatConfigDiffReport _cachedReport;

        #endregion

        #region 生命周期方法

        public override void OnInspectorGUI()
        {
            if (EditorApplication.isCompiling)
            {
                EditorGUILayout.HelpBox("Unity 正在编译脚本中，请稍候...", MessageType.Info);
                return;
            }

            var config = (StatConfigSO)target;
            if (config == null)
            {
                DrawDefaultInspector();
                return;
            }

            // 1. 差异状态与数据校验 (Layout 阶段或初次运行时刷新)
            if (Event.current.type == EventType.Layout || _cachedReport == null)
            {
                _cachedReport = StatCodeGenerator.CheckDiff(config);
            }

            // 顶部差异与诊断状态横幅
            DrawStatusHeader(_cachedReport, config);

            EditorGUILayout.Space(6);

            // 2. 绘制常规默认 Inspector 界面 (保留原生序列化属性排版、拖拽排序与引用槽，兼容子类自由扩展)
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                _cachedReport = StatCodeGenerator.CheckDiff(config);
            }

            EditorGUILayout.Space(10);

            // 3. 底部生成按钮与辅助工具区
            DrawActionButtons(_cachedReport, config);
        }

        #endregion

        #region 私有方法

        private void DrawStatusHeader(StatConfigDiffReport report, StatConfigSO config)
        {
            if (report == null) return;

            if (report.HasDataErrors)
            {
                var errorSummary = string.Join("\n• ", report.ErrorMessages);
                EditorGUILayout.HelpBox($"检测到配置数据异常 (代码生成已阻断)：\n• {errorSummary}", MessageType.Error);
                return;
            }

            if (!report.CodeFilesExist)
            {
                EditorGUILayout.HelpBox($"[代码状态: 尚未生成代码]\n当前输出目录: {report.TargetDirectory}\n尚未检测到生成的 C# 脚本，请点击下方按钮生成代码。", MessageType.Warning);
                return;
            }

            if (!report.IsUpToDate)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("[代码状态: 检测到未同步的配置变动]", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("配置与磁盘代码存在差异，建议重新生成代码以保持强类型同步：", EditorStyles.miniLabel);

                var totalDiffs = report.AddedStats.Count + report.RemovedStats.Count +
                                 report.ModifiedOrReorderedStats.Count +
                                 report.AddedTags.Count + report.RemovedTags.Count;

                _showDiffFoldout = EditorGUILayout.Foldout(_showDiffFoldout, $"变动明细 (共 {totalDiffs} 项差异)", true);
                if (_showDiffFoldout)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < report.AddedStats.Count; i++)
                    {
                        EditorGUILayout.LabelField($"+ 新增属性: {report.AddedStats[i]}", EditorStyles.miniLabel);
                    }
                    for (int i = 0; i < report.RemovedStats.Count; i++)
                    {
                        EditorGUILayout.LabelField($"- 移除属性: {report.RemovedStats[i]}", EditorStyles.miniLabel);
                    }
                    for (int i = 0; i < report.ModifiedOrReorderedStats.Count; i++)
                    {
                        EditorGUILayout.LabelField($"~ 调序/改名: {report.ModifiedOrReorderedStats[i]}", EditorStyles.miniLabel);
                    }
                    for (int i = 0; i < report.AddedTags.Count; i++)
                    {
                        EditorGUILayout.LabelField($"+ 新增标签: {report.AddedTags[i]}", EditorStyles.miniLabel);
                    }
                    for (int i = 0; i < report.RemovedTags.Count; i++)
                    {
                        EditorGUILayout.LabelField($"- 移除标签: {report.RemovedTags[i]}", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                return;
            }

            // 代码与配置完全同步
            var statCount = config.StatDefinitions != null ? config.StatDefinitions.Count : 0;
            var tagCount = config.TagNames != null ? config.TagNames.Count : 0;
            EditorGUILayout.HelpBox($"[代码状态: 已是最新]\nC# 代码与当前配置完全同步 (包含 {statCount} 项属性，{tagCount} 个标签)。\n输出目录: {report.TargetDirectory}", MessageType.Info);
        }

        private void DrawActionButtons(StatConfigDiffReport report, StatConfigSO config)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 主生成按钮
            if (report != null && report.HasDataErrors)
            {
                GUI.enabled = false;
                GUILayout.Button("生成强类型 C# 代码 (请先修复上方配置错误)", GUILayout.Height(36));
                GUI.enabled = true;
            }
            else
            {
                var isOutOfDate = report == null || !report.IsUpToDate;
                GUI.backgroundColor = isOutOfDate ? new Color(0.2f, 0.65f, 1.0f) : new Color(0.85f, 0.95f, 0.85f);
                var buttonText = isOutOfDate
                    ? "★ 生成强类型 C# 代码 (Generate C# Code) ★"
                    : "重新生成强类型 C# 代码 (Regenerate C# Code)";

                if (GUILayout.Button(buttonText, GUILayout.Height(36)))
                {
                    if (StatCodeGenerator.Generate(config))
                    {
                        _cachedReport = StatCodeGenerator.CheckDiff(config);
                        EditorUtility.DisplayDialog("CwcStatSystem", $"成功生成 [{config.ConfigName}] 的专属代码！", "确定");
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(4);

            // 辅助快捷工具行
            EditorGUILayout.BeginHorizontal();

            // 定位生成代码按钮
            if (GUILayout.Button("定位生成代码", GUILayout.Height(24)))
            {
                PingGeneratedCode(report, config);
            }

            // 若存在空引用则显示清理按钮
            if (report != null && report.NullDefinitionIndices.Count > 0)
            {
                GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
                if (GUILayout.Button($"清理空引用 ({report.NullDefinitionIndices.Count})", GUILayout.Height(24)))
                {
                    RemoveNullDefinitions(config);
                    _cachedReport = StatCodeGenerator.CheckDiff(config);
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void PingGeneratedCode(StatConfigDiffReport report, StatConfigSO config)
        {
            var targetDir = report != null && !string.IsNullOrEmpty(report.TargetDirectory)
                ? report.TargetDirectory
                : StatCodeGenerator.ResolveTargetDirectory(config, AssetDatabase.GetAssetPath(config));

            var cleanName = StatCodeGenerator.CleanIdentifier(config.ConfigName);
            if (string.IsNullOrEmpty(cleanName)) cleanName = "Default";

            var statTypePath = $"{targetDir}/{cleanName}StatType.cs";
            var targetAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(statTypePath);
            if (targetAsset != null)
            {
                EditorGUIUtility.PingObject(targetAsset);
                Selection.activeObject = targetAsset;
            }
            else
            {
                Debug.LogWarning($"[CwcStatSystem] 未找到代码文件: {statTypePath}，请先点击生成按钮！");
            }
        }

        private void RemoveNullDefinitions(StatConfigSO config)
        {
            Undo.RecordObject(config, "Remove Null Stat Definitions");

            var so = new SerializedObject(config);
            var listProp = so.FindProperty("_statDefinitions");
            if (listProp != null && listProp.isArray)
            {
                for (int i = listProp.arraySize - 1; i >= 0; i--)
                {
                    var elem = listProp.GetArrayElementAtIndex(i);
                    if (elem.objectReferenceValue == null)
                    {
                        listProp.DeleteArrayElementAtIndex(i);
                    }
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"[CwcStatSystem] 已清理 [{config.ConfigName}] 中的所有空引用。");
            }
        }

        #endregion
    }
}
