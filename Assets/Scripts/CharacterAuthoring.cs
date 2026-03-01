using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// 标记角色初始化完成的组件数据结构。
/// 实现 IEnableableComponent 接口，用于控制组件是否启用。
/// </summary>
public struct InitializeCharacterFlag : IComponentData, IEnableableComponent
{
}

/// <summary>
/// 存储角色移动方向的组件数据结构。
/// 包含一个二维向量表示角色的移动方向。
/// </summary>
public struct CharacterMoveDirection : IComponentData
{
    public float2 Value;
}

/// <summary>
/// 存储角色移动速度的组件数据结构。
/// 包含一个浮点数表示角色的移动速度。
/// </summary>
public struct CharacterMoveSpeed : IComponentData
{
    public float Value;
}

/// <summary>
/// 覆盖角色朝向方向的组件数据结构。
/// 使用 MaterialProperty 特性绑定到材质属性 "_FacingDirection"。
/// </summary>
[MaterialProperty("_FacingDirection")]
public struct FacingDirectionOverride : IComponentData
{
    public float Value;
}

/// <summary>
/// 存储角色最大生命值的组件数据结构。
/// 包含一个整数表示角色的最大生命值。
/// </summary>
public struct CharacterMaxHitPoints : IComponentData
{
    public int Value;
}

/// <summary>
/// 存储角色当前生命值的组件数据结构。
/// 包含一个整数表示角色的当前生命值。
/// </summary>
public struct CharacterCurrentHitPoints : IComponentData
{
    public int Value;
}

/// <summary>
/// 存储本帧伤害数据的缓冲区元素结构。
/// 用于记录角色在当前帧受到的伤害值。
/// </summary>
public struct DamageThisFrame : IBufferElementData
{
    public int Value;
}

/// <summary>
/// 角色授权类，用于在 Unity 编辑器中配置角色属性。
/// 包含移动速度和生命值等可配置字段。
/// </summary>
public class CharacterAuthoring : MonoBehaviour
{
    public float MoveSpeed;
    public int HitPoints;

    /// <summary>
    /// 角色烘焙器类，负责将 MonoBehaviour 数据转换为 ECS 组件。
    /// </summary>
    private class Baker : Baker<CharacterAuthoring>
    {
        /// <summary>
        /// 将 CharacterAuthoring 的数据烘焙到 ECS 实体中。
        /// 添加各种角色相关的组件并设置初始值。
        /// </summary>
        /// <param name="authoring">CharacterAuthoring 实例。</param>
        public override void Bake(CharacterAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<InitializeCharacterFlag>(entity);
            AddComponent<CharacterMoveDirection>(entity);
            AddComponent(entity, new CharacterMoveSpeed
            {
                Value = authoring.MoveSpeed,
            });
            AddComponent(entity, new FacingDirectionOverride
            {
                Value = 1,
            });
            AddComponent(entity, new CharacterMaxHitPoints
            {
                Value = authoring.HitPoints,
            });
            AddComponent(entity, new CharacterCurrentHitPoints
            {
                Value = authoring.HitPoints,
            });
            AddComponent<DamageThisFrame>(entity);
            AddComponent<DestroyEntityFlag>(entity);
            SetComponentEnabled<DestroyEntityFlag>(entity, false);
        }
    }
}

/// <summary>
/// 角色初始化系统，在 InitializationSystemGroup 中运行。
/// 负责初始化角色的物理质量和禁用初始化标志。
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct CharacterInitializationSystem : ISystem
{
    /// <summary>
    /// 每帧更新逻辑，初始化角色的物理质量并禁用初始化标志。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (mass, shouldInitialize) in SystemAPI
                     .Query<RefRW<PhysicsMass>, EnabledRefRW<InitializeCharacterFlag>>())
        {
            mass.ValueRW.InverseInertia = float3.zero;
            shouldInitialize.ValueRW = false;
        }
    }
}

/// <summary>
/// 角色移动系统，负责处理角色的移动逻辑。
/// 更新角色的速度、朝向以及动画状态。
/// </summary>
public partial struct CharacterMoveSystem : ISystem
{
    /// <summary>
    /// 每帧更新逻辑，计算角色移动并向量，并更新相关组件。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (velocity, facingDirection, direction, speed, entity) in SystemAPI
                     .Query<RefRW<PhysicsVelocity>, RefRW<FacingDirectionOverride>, CharacterMoveDirection,
                         CharacterMoveSpeed>().WithEntityAccess())
        {
            var moveStep2d = direction.Value * speed.Value;
            velocity.ValueRW.Linear = new float3(moveStep2d, 0f);

            if (math.abs(moveStep2d.x) > 0.15f)
            {
                facingDirection.ValueRW.Value = math.sign(moveStep2d.x);
            }

            if (SystemAPI.HasComponent<PlayerTag>(entity))
            {
                var animationOverride = SystemAPI.GetComponentRW<AnimationIndexOverride>(entity);
                var animationType = math.lengthsq(moveStep2d) > float.Epsilon
                    ? PlayerAnimationIndex.Movement
                    : PlayerAnimationIndex.Idle;
                animationOverride.ValueRW.Value = (float)animationType;
            }
        }
    }
}

/// <summary>
/// 全局时间更新系统，负责每帧更新全局时间 shader 属性。
/// </summary>
public partial struct GlobalTimeUpdateSystem : ISystem
{
    private static int _globalTimeShaderPropertyID;

    /// <summary>
    /// 系统创建时初始化全局时间 shader 属性 ID。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    public void OnCreate(ref SystemState state)
    {
        _globalTimeShaderPropertyID = Shader.PropertyToID("_GlobalTime");
    }

    /// <summary>
    /// 每帧更新全局时间 shader 属性。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    public void OnUpdate(ref SystemState state)
    {
        Shader.SetGlobalFloat(_globalTimeShaderPropertyID, (float)SystemAPI.Time.ElapsedTime);
    }
}

/// <summary>
/// 处理本帧伤害的系统，负责应用伤害并销毁生命值归零的角色实体。
/// </summary>
public partial struct ProcessDamageThisFrameSystem : ISystem
{
    /// <summary>
    /// 每帧更新逻辑，处理角色受到的伤害并更新生命值。
    /// 如果生命值归零，则标记实体为待销毁状态。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (hitPoints, damageThisFrame, entity) in SystemAPI
                     .Query<RefRW<CharacterCurrentHitPoints>, DynamicBuffer<DamageThisFrame>>()
                     .WithPresent<DestroyEntityFlag>().WithEntityAccess())
        {
            if (damageThisFrame.IsEmpty) continue;
            foreach (var damage in damageThisFrame)
            {
                hitPoints.ValueRW.Value -= damage.Value;
            }

            damageThisFrame.Clear();

            if (hitPoints.ValueRO.Value <= 0)
            {
                SystemAPI.SetComponentEnabled<DestroyEntityFlag>(entity, true);
            }
        }
    }
}