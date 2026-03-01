using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TMG.Survivors
{
    /// <summary>
    /// 主菜单控制器，负责处理主菜单界面的按钮点击事件。
    /// 包括开始游戏和退出游戏的功能。
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button _playButton; // 开始游戏按钮
        [SerializeField] private Button _quitButton; // 退出游戏按钮

        /// <summary>
        /// 当脚本启用时调用，为按钮添加点击事件监听器。
        /// </summary>
        private void OnEnable()
        {
            _playButton.onClick.AddListener(OnPlayButton);
            _quitButton.onClick.AddListener(OnQuitButton);
        }

        /// <summary>
        /// 当脚本禁用时调用，移除按钮的点击事件监听器，避免内存泄漏。
        /// </summary>
        private void OnDisable()
        {
            _playButton.onClick.RemoveAllListeners();
            _quitButton.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// 处理开始游戏按钮的点击事件，加载场景索引为1的关卡。
        /// </summary>
        private void OnPlayButton()
        {
            SceneManager.LoadScene(1);
        }

        /// <summary>
        /// 处理退出游戏按钮的点击事件，退出应用程序。
        /// </summary>
        private void OnQuitButton()
        {
            Application.Quit();
        }
    }
}