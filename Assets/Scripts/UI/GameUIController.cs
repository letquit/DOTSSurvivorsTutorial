using System.Collections;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TMG.Survivors
{
    /// <summary>
    /// 游戏UI控制器，负责管理游戏中的UI元素，如宝石收集显示、暂停菜单、游戏结束面板等。
    /// </summary>
    public class GameUIController : MonoBehaviour
    {
        /// <summary>
        /// 单例实例，用于全局访问GameUIController。
        /// </summary>
        public static GameUIController Instance;

        [SerializeField] private TextMeshProUGUI _gemsCollectedText;
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private Button _quitButton;

        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private Button _pauseResumeButton;
        [SerializeField] private Button _pauseQuitButton;

        /// <summary>
        /// 标记游戏是否处于暂停状态。
        /// </summary>
        private bool _isPaused = false;

        /// <summary>
        /// 在Awake阶段初始化单例实例，并设置初始宝石数量为0。
        /// </summary>
        private void Awake()
        {
            // 检查是否存在多个GameUIController实例，如果存在则销毁新实例并发出警告
            if (Instance != null)
            {
                Debug.LogWarning("Warning: Multiple instances of GameUIController detected. Destroying new instance",
                    Instance);
                return;
            }

            Instance = this;

            UpdateGemsCollectedText(0);
        }

        /// <summary>
        /// 在启用时注册按钮点击事件监听器。
        /// </summary>
        private void OnEnable()
        {
            _quitButton.onClick.AddListener(OnQuitButton);
            _pauseResumeButton.onClick.AddListener(OnResumeButton);
            _pauseQuitButton.onClick.AddListener(OnQuitButton);
        }

        /// <summary>
        /// 在禁用时移除所有按钮点击事件监听器。
        /// </summary>
        private void OnDisable()
        {
            _quitButton.onClick.RemoveAllListeners();
            _pauseResumeButton.onClick.RemoveAllListeners();
            _pauseQuitButton.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// 初始化游戏开始时的UI状态，隐藏游戏结束面板和暂停面板。
        /// </summary>
        private void Start()
        {
            _gameOverPanel.SetActive(false);
            _pausePanel.SetActive(false);
        }

        /// <summary>
        /// 监听键盘输入，按下Escape键时切换游戏暂停状态。
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleGamePause();
            }
        }

        /// <summary>
        /// 切换游戏暂停状态，并更新暂停面板的可见性以及ECS系统的启用状态。
        /// </summary>
        private void ToggleGamePause()
        {
            _isPaused = !_isPaused;
            _pausePanel.SetActive(_isPaused);
            SetEcsEnabled(!_isPaused);
        }

        /// <summary>
        /// 启用或禁用ECS系统组（InitializationSystemGroup 和 SimulationSystemGroup）。
        /// </summary>
        /// <param name="shouldEnable">是否启用ECS系统。</param>
        private void SetEcsEnabled(bool shouldEnable)
        {
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null) return;
            var initializationSystemGroup = defaultWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            initializationSystemGroup.Enabled = shouldEnable;

            var simulationSystemGroup = defaultWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            simulationSystemGroup.Enabled = shouldEnable;
        }

        /// <summary>
        /// 更新界面上显示的宝石收集数量。
        /// </summary>
        /// <param name="gemsCollected">当前收集到的宝石数量。</param>
        public void UpdateGemsCollectedText(int gemsCollected)
        {
            _gemsCollectedText.text = $"{gemsCollected:N0}";
        }

        /// <summary>
        /// 显示游戏结束UI，在延迟一段时间后激活游戏结束面板。
        /// </summary>
        public void ShowGameOverUI()
        {
            StartCoroutine(ShowGameOverUICoroutine());
        }

        /// <summary>
        /// 延迟显示游戏结束面板的协程。
        /// </summary>
        /// <returns>IEnumerator用于协程控制。</returns>
        private IEnumerator ShowGameOverUICoroutine()
        {
            yield return new WaitForSeconds(1.5f);

            _gameOverPanel.SetActive(true);
        }

        /// <summary>
        /// 处理恢复游戏按钮点击事件，调用ToggleGamePause方法取消暂停。
        /// </summary>
        private void OnResumeButton()
        {
            ToggleGamePause();
        }

        /// <summary>
        /// 处理退出游戏按钮点击事件，重新启用ECS系统并加载主场景。
        /// </summary>
        private void OnQuitButton()
        {
            SetEcsEnabled(true);
            SceneManager.LoadScene(0);
        }
    }
}