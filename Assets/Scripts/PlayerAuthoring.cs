using System;
using TMG.Survivors;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 定义一个空结构体，用于标记玩家实体。
/// </summary>
public struct PlayerTag : IComponentData
{
}

/// <summary>
/// 定义相机目标组件，包含相机变换引用。
/// </summary>
public struct CameraTarget : IComponentData
{
    public UnityObjectRef<Transform> CameraTransform;
}

/// <summary>
/// 定义初始化相机目标标签组件，用于标识需要初始化相机目标的实体。
/// </summary>
public struct InitializeCameraTargetTag : IComponentData
{
}

/// <summary>
/// 定义动画索引覆盖组件，用于控制材质属性中的动画索引。
/// </summary>
[MaterialProperty("_AnimationIndex")]
public struct AnimationIndexOverride : IComponentData
{
    public float Value;
}

/// <summary>
/// 定义玩家动画索引枚举，表示不同的动画状态。
/// </summary>
public enum PlayerAnimationIndex : byte
{
    Movement = 0,
    Idle = 1,
    None = byte.MaxValue,
}

/// <summary>
/// 定义玩家攻击数据组件，包含攻击预制体、冷却时间、检测范围和碰撞过滤器。
/// </summary>
public struct PlayerAttackData : IComponentData
{
    public Entity AttackPrefab;
    public float CooldownTime;
    public float3 DetectionSize;
    public CollisionFilter CollisionFilter;
}

/// <summary>
/// 定义玩家冷却到期时间戳组件，记录攻击冷却结束的时间。
/// </summary>
public struct PlayerCooldownExpirationTimestamp : IComponentData
{
    public double Value;
}

/// <summary>
/// 定义收集宝石数量组件，记录玩家当前收集的宝石数。
/// </summary>
public struct GemsCollectedCount : IComponentData
{
    public int Value;
}

/// <summary>
/// 定义更新宝石UI标志组件，启用时触发UI更新。
/// </summary>
public struct UpdateGemUIFlag : IComponentData, IEnableableComponent
{
}

/// <summary>
/// 定义玩家世界UI组件，包含画布变换和血条滑动条引用。
/// </summary>
public struct PlayerWorldUI : ICleanupComponentData
{
    public UnityObjectRef<Transform> CanvasTransform;
    public UnityObjectRef<Slider> HealthBarSlider;
}

/// <summary>
/// 定义玩家世界UI预制体组件，存储UI预制体引用。
/// </summary>
public struct PlayerWorldUIPrefab : IComponentData
{
    public UnityObjectRef<GameObject> Value;
}

/// <summary>
/// 玩家授权类，用于烘焙玩家相关组件到实体中。
/// </summary>
public class PlayerAuthoring : MonoBehaviour
{
    public GameObject AttackPrefab;
    public float CooldownTime;
    public float DetectionSize;
    public GameObject WorldUIPrefab;

    /// <summary>
    /// 烘焙器类，将MonoBehaviour数据转换为ECS组件。
    /// </summary>
    private class Baker : Baker<PlayerAuthoring>
    {
        /// <summary>
        /// 将PlayerAuthoring的数据烘焙到实体中。
        /// </summary>
        /// <param name="authoring">PlayerAuthoring实例。</param>
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
            AddComponent<InitializeCameraTargetTag>(entity);
            AddComponent<CameraTarget>(entity);
            AddComponent<AnimationIndexOverride>(entity);

            var enemyLayer = LayerMask.NameToLayer("Enemy");
            var enemyLayerMask = (uint)math.pow(2, enemyLayer);

            var attackCollisionFilter = new CollisionFilter
            {
                BelongsTo = uint.MaxValue,
                CollidesWith = enemyLayerMask,
            };

            AddComponent(entity, new PlayerAttackData
            {
                AttackPrefab = GetEntity(authoring.AttackPrefab, TransformUsageFlags.Dynamic),
                CooldownTime = authoring.CooldownTime,
                DetectionSize = new float3(authoring.DetectionSize),
                CollisionFilter = attackCollisionFilter
            });
            AddComponent<PlayerCooldownExpirationTimestamp>(entity);
            AddComponent<GemsCollectedCount>(entity);
            AddComponent<UpdateGemUIFlag>(entity);
            AddComponent(entity, new PlayerWorldUIPrefab
            {
                Value = authoring.WorldUIPrefab
            });
        }
    }
}

/// <summary>
/// 相机初始化系统，在初始化阶段设置相机目标。
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct CameraInitializationSystem : ISystem
{
    /// <summary>
    /// 系统创建时调用，要求存在InitializeCameraTargetTag组件。
    /// </summary>
    /// <param name="state">系统状态。</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<InitializeCameraTargetTag>();
    }

    /// <summary>
    /// 每帧更新时执行，初始化相机目标并移除初始化标签。
    /// </summary>
    /// <param name="state">系统状态。</param>
    public void OnUpdate(ref SystemState state)
    {
        if (CameraTargetSingleton.Instance == null) return;
        var cameraTargetTransform = CameraTargetSingleton.Instance.transform;

        var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
        foreach (var (cameraTarget, entity) in SystemAPI.Query<RefRW<CameraTarget>>()
                     .WithAll<InitializeCameraTargetTag, PlayerTag>().WithEntityAccess())
        {
            cameraTarget.ValueRW.CameraTransform = cameraTargetTransform;

            ecb.RemoveComponent<InitializeCameraTargetTag>(entity);
        }

        ecb.Playback(state.EntityManager);
    }
}

/// <summary>
/// 移动相机系统，在变换系统之后更新相机位置。
/// </summary>
[UpdateAfter(typeof(TransformSystemGroup))]
public partial struct MoveCameraSystem : ISystem
{
    /// <summary>
    /// 每帧更新时执行，将相机位置同步到玩家位置。
    /// </summary>
    /// <param name="state">系统状态。</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (transform, cameraTarget) in SystemAPI.Query<LocalToWorld, CameraTarget>().WithAll<PlayerTag>()
                     .WithNone<InitializeCameraTargetTag>())
        {
            cameraTarget.CameraTransform.Value.position = transform.Position;
        }
    }
}

/// <summary>
/// 玩家输入系统，处理玩家移动输入。
/// </summary>
public partial class PlayerInputSystem : SystemBase
{
    private SurvivorsInput _input;

    /// <summary>
    /// 系统创建时调用，初始化输入系统。
    /// </summary>
    protected override void OnCreate()
    {
        _input = new SurvivorsInput();
        _input.Enable();
    }

    /// <summary>
    /// 每帧更新时执行，读取玩家输入并更新角色移动方向。
    /// </summary>
    protected override void OnUpdate()
    {
        var currentInput = (float2)_input.Player.Move.ReadValue<Vector2>();
        foreach (var direction in SystemAPI.Query<RefRW<CharacterMoveDirection>>().WithAll<PlayerTag>())
        {
            direction.ValueRW.Value = currentInput;
        }
    }
}

/// <summary>
/// 玩家攻击系统，处理玩家攻击逻辑。
/// </summary>
public partial struct PlayerAttackSystem : ISystem
{
    /// <summary>
    /// 系统创建时调用，要求存在物理世界单例和固定步长模拟命令缓冲区系统。
    /// </summary>
    /// <param name="state">系统状态。</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
    }

    /// <summary>
    /// 每帧更新时执行，检测敌人并生成攻击实体。
    /// </summary>
    /// <param name="state">系统状态。</param>
    public void OnUpdate(ref SystemState state)
    {
        var elapsedTime = SystemAPI.Time.ElapsedTime;

        var ecbSystem = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
        var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        foreach (var (expirationTimestamp, attackData, transform) in SystemAPI
                     .Query<RefRW<PlayerCooldownExpirationTimestamp>, PlayerAttackData, LocalTransform>())
        {
            if (expirationTimestamp.ValueRO.Value > elapsedTime) continue;

            var spawnPosition = transform.Position;
            var minDetectPosition = spawnPosition - attackData.DetectionSize;
            var maxDetectPosition = spawnPosition + attackData.DetectionSize;

            var aabbInput = new OverlapAabbInput
            {
                Aabb = new Aabb
                {
                    Min = minDetectPosition,
                    Max = maxDetectPosition
                },
                Filter = attackData.CollisionFilter,
            };

            var overlapHits = new NativeList<int>(state.WorldUpdateAllocator);
            if (!physicsWorldSingleton.OverlapAabb(aabbInput, ref overlapHits))
            {
                continue;
            }

            var maxDistanceSq = float.MaxValue;
            var closestEnemyPosition = float3.zero;
            foreach (var overlapHit in overlapHits)
            {
                var curEnemyPosition = physicsWorldSingleton.Bodies[overlapHit].WorldFromBody.pos;
                var distanceToPlayerSq = math.distancesq(spawnPosition.xy, curEnemyPosition.xy);
                if (distanceToPlayerSq < maxDistanceSq)
                {
                    maxDistanceSq = distanceToPlayerSq;
                    closestEnemyPosition = curEnemyPosition;
                }
            }

            var vectorToClosestEnemy = closestEnemyPosition - spawnPosition;
            var angleToClosestEnemy = math.atan2(vectorToClosestEnemy.y, vectorToClosestEnemy.x);
            var spawnOrientation = quaternion.Euler(0f, 0f, angleToClosestEnemy);

            var newAttack = ecb.Instantiate(attackData.AttackPrefab);
            ecb.SetComponent(newAttack, LocalTransform.FromPositionRotation(spawnPosition, spawnOrientation));

            expirationTimestamp.ValueRW.Value = elapsedTime + attackData.CooldownTime;
        }
    }
}

/// <summary>
/// 更新宝石UI系统，根据收集的宝石数量更新UI显示。
/// </summary>
public partial struct UpdateGemUISystem : ISystem
{
    /// <summary>
    /// 每帧更新时执行，更新宝石计数UI并禁用更新标志。
    /// </summary>
    /// <param name="state">系统状态。</param>
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (gemCount, shouldUpdateUI) in SystemAPI.Query<GemsCollectedCount, EnabledRefRW<UpdateGemUIFlag>>())
        {
            GameUIController.Instance.UpdateGemsCollectedText(gemCount.Value);
            shouldUpdateUI.ValueRW = false;
        }
    }
}

/// <summary>
/// 玩家世界UI系统，管理玩家世界UI的创建、更新和销毁。
/// </summary>
public partial struct PlayerWorldUISystem : ISystem
{
    /// <summary>
    /// 每帧更新时执行，处理UI的实例化、位置同步和清理。
    /// </summary>
    /// <param name="state">系统状态。</param>
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

        foreach (var (uiPrefab, entity) in SystemAPI.Query<PlayerWorldUIPrefab>().WithNone<PlayerWorldUI>()
                     .WithEntityAccess())
        {
            var newWorldUI = Object.Instantiate(uiPrefab.Value.Value);
            ecb.AddComponent(entity, new PlayerWorldUI
            {
                CanvasTransform = newWorldUI.transform,
                HealthBarSlider = newWorldUI.GetComponentInChildren<Slider>()
            });
        }

        foreach (var (transform, worldUI, currentHitPoints, maxHitPoints) in SystemAPI
                     .Query<LocalToWorld, PlayerWorldUI, CharacterCurrentHitPoints, CharacterMaxHitPoints>())
        {
            worldUI.CanvasTransform.Value.position = transform.Position;
            var healthValue = (float)currentHitPoints.Value / maxHitPoints.Value;
            worldUI.HealthBarSlider.Value.value = healthValue;
        }

        foreach (var (worldUI, entity) in SystemAPI.Query<PlayerWorldUI>().WithNone<LocalToWorld>().WithEntityAccess())
        {
            if (worldUI.CanvasTransform.Value != null)
            {
                Object.Destroy(worldUI.CanvasTransform.Value.gameObject);
            }

            ecb.RemoveComponent<PlayerWorldUI>(entity);
        }

        ecb.Playback(state.EntityManager);
    }
}