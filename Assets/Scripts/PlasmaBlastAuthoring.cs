using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// 定义一个组件数据结构，用于存储等离子爆炸的相关属性。
/// </summary>
public struct PlasmaBlastData : IComponentData
{
    /// <summary>
    /// 等离子爆炸的移动速度。
    /// </summary>
    public float MoveSpeed;

    /// <summary>
    /// 等离子爆炸的攻击伤害值。
    /// </summary>
    public int AttackDamage;
}

/// <summary>
/// 用于在Unity编辑器中配置等离子爆炸属性的MonoBehaviour脚本。
/// </summary>
public class PlasmaBlastAuthoring : MonoBehaviour
{
    /// <summary>
    /// 在编辑器中可配置的等离子爆炸移动速度。
    /// </summary>
    public float MoveSpeed;

    /// <summary>
    /// 在编辑器中可配置的等离子爆炸攻击伤害值。
    /// </summary>
    public int AttackDamage;

    /// <summary>
    /// 自定义Baker类，用于将PlasmaBlastAuthoring的数据烘焙到实体系统中。
    /// </summary>
    private class Baker : Baker<PlasmaBlastAuthoring>
    {
        /// <summary>
        /// 将PlasmaBlastAuthoring组件的数据转换为实体组件并添加到实体中。
        /// </summary>
        /// <param name="authoring">当前的PlasmaBlastAuthoring实例。</param>
        public override void Bake(PlasmaBlastAuthoring authoring)
        {
            // 创建一个动态变换使用的实体
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // 添加PlasmaBlastData组件，并设置其属性
            AddComponent(entity, new PlasmaBlastData
            {
                MoveSpeed = authoring.MoveSpeed,
                AttackDamage = authoring.AttackDamage
            });

            // 添加销毁标记组件，并默认禁用
            AddComponent<DestroyEntityFlag>(entity);
            SetComponentEnabled<DestroyEntityFlag>(entity, false);
        }
    }
}

/// <summary>
/// 系统类，负责更新等离子爆炸的位置。
/// </summary>
public partial struct MovePlasmaBlastSystem : ISystem
{
    /// <summary>
    /// 每帧更新等离子爆炸的位置。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    public void OnUpdate(ref SystemState state)
    {
        // 获取时间增量
        var deltaTime = SystemAPI.Time.DeltaTime;

        // 遍历所有具有LocalTransform和PlasmaBlastData组件的实体
        foreach (var (transform, data) in SystemAPI.Query<RefRW<LocalTransform>, PlasmaBlastData>())
        {
            // 根据移动速度和方向更新位置
            transform.ValueRW.Position += transform.ValueRO.Right() * data.MoveSpeed * deltaTime;
        }
    }
}

/// <summary>
/// 系统类，处理等离子爆炸与敌人的碰撞逻辑。
/// 更新顺序：在物理模拟之后、后物理系统之前执行。
/// </summary>
[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsSimulationGroup))]
[UpdateBefore(typeof(AfterPhysicsSystemGroup))]
public partial struct PlasmaBlastAttackSystem : ISystem
{
    /// <summary>
    /// 系统创建时初始化依赖项。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    public void OnCreate(ref SystemState state)
    {
        // 要求SimulationSingleton组件存在才能运行此系统
        state.RequireForUpdate<SimulationSingleton>();
    }

    /// <summary>
    /// 每帧检查等离子爆炸是否与敌人发生碰撞，并触发攻击逻辑。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    public void OnUpdate(ref SystemState state)
    {
        // 初始化攻击任务
        var attackJob = new PlasmaBlastAttackJob
        {
            PlasmaBlastLookup = SystemAPI.GetComponentLookup<PlasmaBlastData>(true),
            EnemyLookup = SystemAPI.GetComponentLookup<EnemyTag>(true),
            DamageBufferLookup = SystemAPI.GetBufferLookup<DamageThisFrame>(),
            DestroyEntityLookup = SystemAPI.GetComponentLookup<DestroyEntityFlag>(),
        };

        // 获取物理模拟单例
        var simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();

        // 调度攻击任务并传递依赖关系
        state.Dependency = attackJob.Schedule(simulationSingleton, state.Dependency);
    }
}

/// <summary>
/// 触发事件作业类，处理等离子爆炸与敌人之间的碰撞检测和攻击逻辑。
/// </summary>
public struct PlasmaBlastAttackJob : ITriggerEventsJob
{
    /// <summary>
    /// 只读查找表，用于获取等离子爆炸组件数据。
    /// </summary>
    [ReadOnly] public ComponentLookup<PlasmaBlastData> PlasmaBlastLookup;

    /// <summary>
    /// 只读查找表，用于判断实体是否为敌人。
    /// </summary>
    [ReadOnly] public ComponentLookup<EnemyTag> EnemyLookup;

    /// <summary>
    /// 缓冲区查找表，用于向敌人添加伤害数据。
    /// </summary>
    public BufferLookup<DamageThisFrame> DamageBufferLookup;

    /// <summary>
    /// 组件查找表，用于启用或禁用实体的销毁标记。
    /// </summary>
    public ComponentLookup<DestroyEntityFlag> DestroyEntityLookup;

    /// <summary>
    /// 执行触发事件逻辑，检测等离子爆炸与敌人的碰撞并应用伤害。
    /// </summary>
    /// <param name="triggerEvent">触发事件数据。</param>
    public void Execute(TriggerEvent triggerEvent)
    {
        Entity plasmaBlastEntity;
        Entity enemyEntity;

        // 判断哪个实体是等离子爆炸，哪个是敌人
        if (PlasmaBlastLookup.HasComponent(triggerEvent.EntityA) && EnemyLookup.HasComponent(triggerEvent.EntityB))
        {
            plasmaBlastEntity = triggerEvent.EntityA;
            enemyEntity = triggerEvent.EntityB;
        }
        else if (PlasmaBlastLookup.HasComponent(triggerEvent.EntityB) && EnemyLookup.HasComponent(triggerEvent.EntityA))
        {
            plasmaBlastEntity = triggerEvent.EntityB;
            enemyEntity = triggerEvent.EntityA;
        }
        else
        {
            return; // 如果都不是有效组合，则跳过
        }

        // 获取攻击伤害值
        var attackDamage = PlasmaBlastLookup[plasmaBlastEntity].AttackDamage;

        // 向敌人添加伤害数据
        var enemyDamageBuffer = DamageBufferLookup[enemyEntity];
        enemyDamageBuffer.Add(new DamageThisFrame
        {
            Value = attackDamage
        });

        // 启用等离子爆炸实体的销毁标记
        DestroyEntityLookup.SetComponentEnabled(plasmaBlastEntity, true);
    }
}