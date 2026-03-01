using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// 定义一个空结构体，用于标记敌人实体。
/// </summary>
public struct EnemyTag : IComponentData
{
}

/// <summary>
/// 定义敌人的攻击数据组件，包含攻击力和冷却时间。
/// </summary>
public struct EnemyAttackData : IComponentData
{
    /// <summary>
    /// 敌人每次攻击造成的伤害点数。
    /// </summary>
    public int HitPoints;

    /// <summary>
    /// 攻击后的冷却时间（秒）。
    /// </summary>
    public float CooldownTime;
}

/// <summary>
/// 定义敌人攻击冷却结束的时间戳组件，并实现启用/禁用功能。
/// </summary>
public struct EnemyCooldownExpirationTimestamp : IComponentData, IEnableableComponent
{
    /// <summary>
    /// 冷却结束的时间戳（以秒为单位）。
    /// </summary>
    public double Value;
}

/// <summary>
/// 定义掉落物预制体组件，存储掉落物的实体引用。
/// </summary>
public struct GemPrefab : IComponentData
{
    /// <summary>
    /// 掉落物的实体引用。
    /// </summary>
    public Entity Value;
}

/// <summary>
/// 敌人创作类，用于在Unity编辑器中配置敌人属性并烘焙为ECS实体。
/// </summary>
[RequireComponent(typeof(CharacterAuthoring))]
public class EnemyAuthoring : MonoBehaviour
{
    /// <summary>
    /// 敌人每次攻击造成的伤害点数。
    /// </summary>
    public int AttackDamage;

    /// <summary>
    /// 攻击后的冷却时间（秒）。
    /// </summary>
    public float CooldownTime;

    /// <summary>
    /// 掉落物的预制体对象。
    /// </summary>
    public GameObject GemPrefab;

    /// <summary>
    /// 自定义烘焙器，将EnemyAuthoring组件的数据转换为ECS组件。
    /// </summary>
    private class Baker : Baker<EnemyAuthoring>
    {
        /// <summary>
        /// 将EnemyAuthoring组件的数据烘焙到对应的ECS实体上。
        /// </summary>
        /// <param name="authoring">EnemyAuthoring组件实例。</param>
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<EnemyTag>(entity);
            AddComponent(entity, new EnemyAttackData
            {
                HitPoints = authoring.AttackDamage,
                CooldownTime = authoring.CooldownTime
            });
            AddComponent<EnemyCooldownExpirationTimestamp>(entity);
            SetComponentEnabled<EnemyCooldownExpirationTimestamp>(entity, false);
            AddComponent(entity, new GemPrefab
            {
                Value = GetEntity(authoring.GemPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}

/// <summary>
/// 敌人移动系统，控制敌人向玩家方向移动。
/// </summary>
public partial struct EnemyMoveToPlayerSystem : ISystem
{
    /// <summary>
    /// 系统创建时调用，要求系统依赖于PlayerTag组件的存在。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
    }

    /// <summary>
    /// 每帧更新时调用，计算敌人向玩家移动的方向并调度任务。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    public void OnUpdate(ref SystemState state)
    {
        var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
        var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position.xy;
        var moveToPlayerJob = new EnemyMoveToPlayerJob
        {
            PlayerPosition = playerPosition
        };

        state.Dependency = moveToPlayerJob.ScheduleParallel(state.Dependency);
    }
}

/// <summary>
/// 敌人移动任务，计算敌人向玩家移动的方向。
/// </summary>
[BurstCompile]
[WithAll(typeof(EnemyTag))]
public partial struct EnemyMoveToPlayerJob : IJobEntity
{
    /// <summary>
    /// 玩家的位置坐标。
    /// </summary>
    public float2 PlayerPosition;

    /// <summary>
    /// 执行任务逻辑，计算敌人向玩家移动的方向。
    /// </summary>
    /// <param name="direction">角色移动方向组件的引用。</param>
    /// <param name="transform">本地变换组件。</param>
    private void Execute(ref CharacterMoveDirection direction, in LocalTransform transform)
    {
        var vectorToPlayer = PlayerPosition - transform.Position.xy;
        direction.Value = math.normalize(vectorToPlayer);
    }
}

/// <summary>
/// 敌人攻击系统，处理敌人与玩家之间的碰撞事件并执行攻击逻辑。
/// </summary>
[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsSimulationGroup))]
[UpdateBefore(typeof(AfterPhysicsSystemGroup))]
public partial struct EnemyAttackSystem : ISystem
{
    /// <summary>
    /// 系统创建时调用，要求系统依赖于SimulationSingleton组件的存在。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationSingleton>();
    }

    /// <summary>
    /// 每帧更新时调用，检查冷却状态并调度攻击任务。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var elapsedTime = SystemAPI.Time.ElapsedTime;
        foreach (var (expirationTimestamp, cooldownEnabled) in SystemAPI
                     .Query<EnemyCooldownExpirationTimestamp, EnabledRefRW<EnemyCooldownExpirationTimestamp>>())
        {
            if (expirationTimestamp.Value > elapsedTime) continue;
            cooldownEnabled.ValueRW = false;
        }

        var attackJob = new EnemyAttackJob
        {
            PlayerLookup = SystemAPI.GetComponentLookup<PlayerTag>(true),
            AttackDataLookup = SystemAPI.GetComponentLookup<EnemyAttackData>(true),
            CooldownLookup = SystemAPI.GetComponentLookup<EnemyCooldownExpirationTimestamp>(),
            DamageBufferLookup = SystemAPI.GetBufferLookup<DamageThisFrame>(),
            ElapsedTime = elapsedTime
        };

        var simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();
        state.Dependency = attackJob.Schedule(simulationSingleton, state.Dependency);
    }
}

/// <summary>
/// 敌人攻击任务，处理敌人与玩家之间的碰撞事件并执行攻击逻辑。
/// </summary>
public struct EnemyAttackJob : ICollisionEventsJob
{
    /// <summary>
    /// 只读查找表，用于判断实体是否具有PlayerTag组件。
    /// </summary>
    [ReadOnly] public ComponentLookup<PlayerTag> PlayerLookup;

    /// <summary>
    /// 只读查找表，用于获取敌人的攻击数据。
    /// </summary>
    [ReadOnly] public ComponentLookup<EnemyAttackData> AttackDataLookup;

    /// <summary>
    /// 查找表，用于设置敌人的冷却状态。
    /// </summary>
    public ComponentLookup<EnemyCooldownExpirationTimestamp> CooldownLookup;

    /// <summary>
    /// 缓冲区查找表，用于记录玩家受到的伤害。
    /// </summary>
    public BufferLookup<DamageThisFrame> DamageBufferLookup;

    /// <summary>
    /// 当前游戏运行时间（秒）。
    /// </summary>
    public double ElapsedTime;

    /// <summary>
    /// 处理碰撞事件，判断是否为敌人攻击玩家，并执行攻击逻辑。
    /// </summary>
    /// <param name="collisionEvent">碰撞事件数据。</param>
    public void Execute(CollisionEvent collisionEvent)
    {
        Entity playerEntity;
        Entity enemyEntity;

        // 判断碰撞双方中哪一个是玩家，哪一个是敌人
        if (PlayerLookup.HasComponent(collisionEvent.EntityA) && AttackDataLookup.HasComponent(collisionEvent.EntityB))
        {
            playerEntity = collisionEvent.EntityA;
            enemyEntity = collisionEvent.EntityB;
        }
        else if (PlayerLookup.HasComponent(collisionEvent.EntityB) &&
                 AttackDataLookup.HasComponent(collisionEvent.EntityA))
        {
            playerEntity = collisionEvent.EntityB;
            enemyEntity = collisionEvent.EntityA;
        }
        else
        {
            return;
        }

        // 如果敌人处于冷却状态，则跳过攻击
        if (CooldownLookup.IsComponentEnabled(enemyEntity)) return;

        // 获取敌人的攻击数据并设置冷却状态
        var attackData = AttackDataLookup[enemyEntity];
        CooldownLookup[enemyEntity] = new EnemyCooldownExpirationTimestamp
        {
            Value = ElapsedTime + attackData.CooldownTime,
        };
        CooldownLookup.SetComponentEnabled(enemyEntity, true);

        // 向玩家添加伤害记录
        var playerDamageBuffer = DamageBufferLookup[playerEntity];
        playerDamageBuffer.Add(new DamageThisFrame
        {
            Value = attackData.HitPoints
        });
    }
}