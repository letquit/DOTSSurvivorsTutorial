using System;
using UnityEngine;

/// <summary>
/// CameraTargetSingleton 是一个单例模式的 MonoBehaviour 类，用于确保场景中只有一个 CameraTargetSingleton 实例存在。
/// 该类通过 Awake 方法实现单例逻辑，并在检测到多个实例时销毁新创建的实例并输出警告日志。
/// </summary>
public class CameraTargetSingleton : MonoBehaviour
{
    /// <summary>
    /// 静态实例字段，用于存储 CameraTargetSingleton 的唯一实例。
    /// </summary>
    public static CameraTargetSingleton Instance;

    /// <summary>
    /// Unity 生命周期方法，在对象初始化时调用。
    /// 用于实现单例逻辑：检查是否已存在实例，如果存在则销毁当前对象并输出警告日志；
    /// 如果不存在，则将当前对象设为唯一实例。
    /// </summary>
    public void Awake()
    {
        // 检查是否已存在实例
        if (Instance != null)
        {
            // 输出警告日志，提示检测到多个实例
            Debug.LogWarning("Warning multiple instances of CameraTargetSingleton detected. Destroying new instance.",
                Instance);
            // 销毁当前游戏对象
            Destroy(gameObject);
            return;
        }

        // 将当前实例设为唯一实例
        Instance = this;
    }
}