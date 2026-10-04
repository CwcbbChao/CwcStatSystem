using System.Collections.Generic;
using UnityEngine;

namespace Cwcbb.Tools.CwcStatSystem
{
    /// <summary>
    /// 全局属性容器帧末自动刷新驱动器 (单周期防抖与延迟对齐，零 GC 运行)
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class StatUpdateDriver : MonoBehaviour
    {
        private static List<StatContainer> _dirtyContainers = new List<StatContainer>();
        private static List<StatContainer> _activeBuffer = new List<StatContainer>();
        private static StatUpdateDriver _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void LateUpdate()
        {
            FlushInternal();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 将变脏的属性容器登记入待刷新集合 (O(1) 标志位防重，零堆分配)
        /// </summary>
        public static void RegisterDirty(StatContainer container)
        {
            if (container == null || container._isInDriverQueue)
            {
                return;
            }

            container._isInDriverQueue = true;
            _dirtyContainers.Add(container);
        }

        /// <summary>
        /// 从待刷新集合中注销属性容器 (容器销毁或无观察者时调用)
        /// </summary>
        public static void Unregister(StatContainer container)
        {
            if (container == null || !container._isInDriverQueue)
            {
                return;
            }

            container._isInDriverQueue = false;
            _dirtyContainers.Remove(container);
        }

        /// <summary>
        /// 立即执行一次全局刷新 (供测试、无头服务器或外部逻辑手动驱动)
        /// </summary>
        public static void FlushAll()
        {
            FlushInternal();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _dirtyContainers.Clear();
            _activeBuffer.Clear();
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("[CwcStatSystem] UpdateDriver")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _instance = go.AddComponent<StatUpdateDriver>();
        }

        private static void FlushInternal()
        {
            if (_dirtyContainers.Count == 0)
            {
                return;
            }

            // 双缓冲指针交换，零 GC、零集合拷贝，防止遍历过程中级联添加修改集合
            var temp = _activeBuffer;
            _activeBuffer = _dirtyContainers;
            _dirtyContainers = temp;
            _dirtyContainers.Clear();

            var count = _activeBuffer.Count;
            for (var i = 0; i < count; i++)
            {
                var container = _activeBuffer[i];
                if (container != null)
                {
                    container._isInDriverQueue = false;
                    if (container.IsDirty)
                    {
                        container.EnsureClean();
                    }
                }
            }

            _activeBuffer.Clear();
        }
    }
}
