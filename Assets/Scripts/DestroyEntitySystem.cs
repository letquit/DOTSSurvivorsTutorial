using System;
using TMG.Survivors;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// 定义一个用于标记实体需要被销毁的组件数据结构。
/// 实现 IComponentData 接口以支持 ECS 系统使用，并实现 IEnableableComponent 接口以允许启用/禁用该组件。
/// </summary>
public struct DestroyEntityFlag : IComponentData, IEnableableComponent
{
}

/// <summary>
/// 负责处理带有 DestroyEntityFlag 组件的实体的系统。
/// 在模拟系统组中最后执行，并在 EndSimulationEntityCommandBufferSystem 之前运行。
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
public partial struct DestroyEntitySystem : ISystem
{
    /// <summary>
    /// 系统创建时调用的方法。
    /// 确保 BeginSimulationEntityCommandBufferSystem 和 EndSimulationEntityCommandBufferSystem 已存在并可更新。
    /// </summary>
    /// <param name="state">系统的状态引用。</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    /// <summary>
    /// 每帧更新时调用的方法。
    /// 遍历所有带有 DestroyEntityFlag 组件的实体，根据条件执行特定逻辑（如显示游戏结束 UI 或生成新宝石），然后销毁实体。
    /// </summary>
    /// <param name="state">系统的状态引用。</param>
    public void OnUpdate(ref SystemState state)
    {
        // 获取 EndSimulationEntityCommandBufferSystem 的命令缓冲区
        var endEcbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var endEcb = endEcbSystem.CreateCommandBuffer(state.WorldUnmanaged);

        // 获取 BeginSimulationEntityCommandBufferSystem 的命令缓冲区
        var beginEcbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var beginEcb = beginEcbSystem.CreateCommandBuffer(state.WorldUnmanaged);

        // 遍历所有带有 DestroyEntityFlag 组件的实体
        foreach (var (_, entity) in SystemAPI.Query<DestroyEntityFlag>().WithEntityAccess())
        {
            // 如果实体具有 PlayerTag 组件，则显示游戏结束 UI
            if (SystemAPI.HasComponent<PlayerTag>(entity))
            {
                GameUIController.Instance.ShowGameOverUI();
            }

            // 如果实体具有 GemPrefab 组件，则实例化一个新的宝石预制体
            if (SystemAPI.HasComponent<GemPrefab>(entity))
            {
                var gemPrefab = SystemAPI.GetComponent<GemPrefab>(entity).Value;
                var newGem = beginEcb.Instantiate(gemPrefab);

                // 设置新宝石的位置为原实体的位置
                var spawnPosition = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                beginEcb.SetComponent(newGem, LocalTransform.FromPosition(spawnPosition));
            }

            // 销毁当前实体
            endEcb.DestroyEntity(entity);
        }
    }
}