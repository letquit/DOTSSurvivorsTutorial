using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

/// <summary>
/// 定义一个空的组件标签，用于标记实体为宝石（Gem）。
/// </summary>
public struct GemTag : IComponentData
{
}

/// <summary>
/// 用于在Unity编辑器中创建宝石实体的MonoBehaviour脚本。
/// 通过Baker类将GameObject转换为ECS实体，并添加相关组件。
/// </summary>
public class GemAuthoring : MonoBehaviour
{
    /// <summary>
    /// 自定义Baker类，负责将GemAuthoring组件烘焙为ECS实体。
    /// </summary>
    private class Baker : Baker<GemAuthoring>
    {
        /// <summary>
        /// 将GemAuthoring组件关联的GameObject转换为ECS实体，并添加必要的组件。
        /// </summary>
        /// <param name="authoring">当前的GemAuthoring实例。</param>
        public override void Bake(GemAuthoring authoring)
        {
            // 创建一个动态变换用途的实体
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // 添加GemTag组件，标记该实体为宝石
            AddComponent<GemTag>(entity);

            // 添加DestroyEntityFlag组件，用于控制实体销毁逻辑
            AddComponent<DestroyEntityFlag>(entity);

            // 默认禁用DestroyEntityFlag组件
            SetComponentEnabled<DestroyEntityFlag>(entity, false);
        }
    }
}

/// <summary>
/// 系统类，用于处理宝石收集逻辑。
/// 在每次系统更新时调度CollectGamJob作业。
/// </summary>
public partial struct CollectGemSystem : ISystem
{
    /// <summary>
    /// 系统初始化方法，在系统创建时调用。
    /// 要求系统依赖于SimulationSingleton组件的存在。
    /// </summary>
    /// <param name="state">系统的状态引用。</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationSingleton>();
    }

    /// <summary>
    /// 系统更新方法，在每一帧调用。
    /// 调度CollectGamJob作业以处理触发事件。
    /// </summary>
    /// <param name="state">系统的状态引用。</param>
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 初始化CollectGamJob作业并设置所需的数据查找表
        var newCollectJob = new CollectGamJob
        {
            GemLookup = SystemAPI.GetComponentLookup<GemTag>(true),
            GemsCollectedLookup = SystemAPI.GetComponentLookup<GemsCollectedCount>(),
            DestroyEntityLookup = SystemAPI.GetComponentLookup<DestroyEntityFlag>(),
            UpdateGemUILookup = SystemAPI.GetComponentLookup<UpdateGemUIFlag>()
        };

        // 获取SimulationSingleton单例
        var simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();

        // 调度作业并更新依赖关系
        state.Dependency = newCollectJob.Schedule(simulationSingleton, state.Dependency);
    }
}

/// <summary>
/// 处理触发事件的作业结构体，用于检测宝石与玩家的碰撞并执行收集逻辑。
/// </summary>
[BurstCompile]
public struct CollectGamJob : ITriggerEventsJob
{
    /// <summary>
    /// 只读查找表，用于检查实体是否具有GemTag组件。
    /// </summary>
    [ReadOnly] public ComponentLookup<GemTag> GemLookup;

    /// <summary>
    /// 查找表，用于访问和修改GemsCollectedCount组件。
    /// </summary>
    public ComponentLookup<GemsCollectedCount> GemsCollectedLookup;

    /// <summary>
    /// 查找表，用于访问和修改DestroyEntityFlag组件。
    /// </summary>
    public ComponentLookup<DestroyEntityFlag> DestroyEntityLookup;

    /// <summary>
    /// 查找表，用于访问和修改UpdateGemUIFlag组件。
    /// </summary>
    public ComponentLookup<UpdateGemUIFlag> UpdateGemUILookup;

    /// <summary>
    /// 执行触发事件逻辑，检测宝石与玩家的碰撞并更新相关数据。
    /// </summary>
    /// <param name="triggerEvent">触发事件数据。</param>
    public void Execute(TriggerEvent triggerEvent)
    {
        Entity gemEntity;
        Entity playerEntity;

        // 判断哪个实体是宝石，哪个是玩家
        if (GemLookup.HasComponent(triggerEvent.EntityA) && GemsCollectedLookup.HasComponent(triggerEvent.EntityB))
        {
            gemEntity = triggerEvent.EntityA;
            playerEntity = triggerEvent.EntityB;
        }
        else if (GemLookup.HasComponent(triggerEvent.EntityB) && GemsCollectedLookup.HasComponent(triggerEvent.EntityA))
        {
            gemEntity = triggerEvent.EntityB;
            playerEntity = triggerEvent.EntityA;
        }
        else
        {
            // 如果两个实体都不是宝石或玩家，则直接返回
            return;
        }

        // 增加玩家收集的宝石数量
        var gemsCollected = GemsCollectedLookup[playerEntity];
        gemsCollected.Value += 1;
        GemsCollectedLookup[playerEntity] = gemsCollected;

        // 启用更新UI标志，通知UI系统刷新显示
        UpdateGemUILookup.SetComponentEnabled(playerEntity, true);

        // 启用销毁标志，准备销毁宝石实体
        DestroyEntityLookup.SetComponentEnabled(gemEntity, true);
    }
}