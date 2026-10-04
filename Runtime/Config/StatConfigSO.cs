using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 独立的属性系统总配置资产 (类似 New Input System 的 InputActions，独立驱动专属代码生成)
    /// </summary>
    [CreateAssetMenu(fileName = "StatConfiguration", menuName = "CwcStatSystem/Stat Configuration", order = 0)]
    public class StatConfigSO : ScriptableObject
    {
        [Tooltip("配置集前缀名称 (例如 Combat，将生成 CombatStatType, CombatTag, CombatStatRegistry 等)")]
        [SerializeField] private string _configName = "Combat";

        [Tooltip("生成的代码所归属的 C# 命名空间")]
        [SerializeField] private string _codeNamespace = "Game.Stats";

        [Tooltip("此系统包含的所有属性定义资产")]
        [SerializeField] private List<StatDefinitionSO> _statDefinitions = new List<StatDefinitionSO>();

        [Tooltip("此系统包含的标签名称列表 (每个配置独占 64 位 TagMask 空间，如 Critical, Frozen, Burning, Boss)")]
        [SerializeField] private List<string> _tagNames = new List<string>();

        [Tooltip("可选：指定生成的代码输出目录 (若为空则默认在资产同级 Generated 目录下)")]
        [SerializeField] private string _customOutputDirectory = "";

        [Tooltip("记录上次生成的目录路径，当资产移动时用于自动清理旧残留代码")]
        [SerializeField, HideInInspector] private string _lastGeneratedDirectory = "";

        [Tooltip("记录上次生成时的配置内容特征哈希，用于常态准确判断是否已生成且是否与配置同步")]
        [SerializeField, HideInInspector] private string _lastGeneratedHash = "";

        public string ConfigName => _configName;
        public string CodeNamespace => _codeNamespace;
        
        /// <summary>
        /// 派生类可重写此属性以提供预设的代码输出目录（当 _customOutputDirectory 为空时生效）
        /// </summary>
        public virtual string DefaultOutputDirectory => string.Empty;

        public string CustomOutputDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(_customOutputDirectory))
                {
                    return _customOutputDirectory;
                }
                return DefaultOutputDirectory;
            }
        }

        public IReadOnlyList<StatDefinitionSO> StatDefinitions => _statDefinitions;
        public IReadOnlyList<string> TagNames => _tagNames;

        public string LastGeneratedDirectory
        {
            get => _lastGeneratedDirectory;
            set => _lastGeneratedDirectory = value;
        }

        public string LastGeneratedHash
        {
            get => _lastGeneratedHash;
            set => _lastGeneratedHash = value;
        }

        public StatDefinitionData GetDefinitionData(int statId)
        {
            if (statId >= 0 && statId < _statDefinitions.Count)
            {
                var def = _statDefinitions[statId];
                if (def != null)
                {
                    return def.ToData();
                }
            }

            return StatDefinitionData.Default;
        }
    }
}
