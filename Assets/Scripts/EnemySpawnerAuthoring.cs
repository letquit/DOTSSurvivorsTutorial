using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;
using UnityEngine;

/// <summary>
/// 定义敌人的生成数据结构，用于存储敌人预制体、生成间隔和生成距离。
/// </summary>
public struct EnemySpawnData : IComponentData
{
    /// <summary>
    /// 敌人预制体的实体引用。
    /// </summary>
    public Entity EnemyPrefab;

    /// <summary>
    /// 敌人生成的时间间隔（秒）。
    /// </summary>
    public float SpawnInterval;

    /// <summary>
    /// 敌人生成的距离（相对于玩家位置）。
    /// </summary>
    public float SpawnDistance;
}

/// <summary>
/// 定义敌人的生成状态结构，用于跟踪生成计时器和随机数生成器。
/// </summary>
public struct EnemySpawnState : IComponentData
{
    /// <summary>
    /// 当前生成计时器的剩余时间。
    /// </summary>
    public float SpawnTimer;

    /// <summary>
    /// 用于生成随机角度的随机数生成器。
    /// </summary>
    public Random Random;
}

/// <summary>
/// 敌人生成器的 MonoBehaviour 组件，用于在 Unity 编辑器中配置生成参数。
/// </summary>
public class EnemySpawnerAuthoring : MonoBehaviour
{
    /// <summary>
    /// 敌人预制体的游戏对象引用。
    /// </summary>
    public GameObject EnemyPrefab;

    /// <summary>
    /// 敌人生成的时间间隔（秒）。
    /// </summary>
    public float SpawnInterval;

    /// <summary>
    /// 敌人生成的距离（相对于玩家位置）。
    /// </summary>
    public float SpawnDistance;

    /// <summary>
    /// 随机数生成器的种子值。
    /// </summary>
    public uint RandomSeed;

    /// <summary>
    /// Baker 类负责将 MonoBehaviour 数据转换为 ECS 组件数据。
    /// </summary>
    private class Baker : Baker<EnemySpawnerAuthoring>
    {
        /// <summary>
        /// 将 EnemySpawnerAuthoring 的数据烘焙到 ECS 实体中。
        /// </summary>
        /// <param name="authoring">EnemySpawnerAuthoring 组件实例。</param>
        public override void Bake(EnemySpawnerAuthoring authoring)
        {
            // 创建一个与当前 GameObject 关联的实体
            var entity = GetEntity(TransformUsageFlags.None);

            // 添加 EnemySpawnData 组件并初始化数据
            AddComponent(entity, new EnemySpawnData
            {
                EnemyPrefab = GetEntity(authoring.EnemyPrefab, TransformUsageFlags.Dynamic),
                SpawnInterval = authoring.SpawnInterval,
                SpawnDistance = authoring.SpawnDistance
            });

            // 添加 EnemySpawnState 组件并初始化数据
            AddComponent(entity, new EnemySpawnState
            {
                SpawnTimer = 0,
                Random = Random.CreateFromIndex(authoring.RandomSeed)
            });
        }
    }
}

/// <summary>
/// 敌人生成系统，负责根据配置定时生成敌人。
/// </summary>
public partial struct EnemySpawnSystem : ISystem
{
    /// <summary>
    /// 系统创建时调用，设置依赖项以确保系统正常运行。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    public void OnCreate(ref SystemState state)
    {
        // 要求 PlayerTag 组件存在才能运行此系统
        state.RequireForUpdate<PlayerTag>();

        // 要求 BeginInitializationEntityCommandBufferSystem 存在才能运行此系统
        state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
    }

    /// <summary>
    /// 每帧更新逻辑，处理敌人生成。
    /// </summary>
    /// <param name="state">系统状态引用。</param>
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 获取帧间时间差
        var deltaTime = SystemAPI.Time.DeltaTime;

        // 获取命令缓冲区系统单例
        var ecbSystem = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();

        // 创建命令缓冲区
        var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);

        // 获取玩家实体及其位置
        var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
        var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        // 遍历所有具有 EnemySpawnState 和 EnemySpawnData 的实体
        foreach (var (spawnState, spawnData) in SystemAPI.Query<RefRW<EnemySpawnState>, EnemySpawnData>())
        {
            // 更新生成计时器
            spawnState.ValueRW.SpawnTimer -= deltaTime;

            // 如果计时器未归零则跳过本次循环
            if (spawnState.ValueRW.SpawnTimer > 0f) continue;

            // 重置生成计时器
            spawnState.ValueRW.SpawnTimer = spawnData.SpawnInterval;

            // 实例化新敌人
            var newEnemy = ecb.Instantiate(spawnData.EnemyPrefab);

            // 计算随机生成角度
            var spawnAngle = spawnState.ValueRW.Random.NextFloat(0, math.TAU);

            // 计算生成点坐标
            var spawnPoint = new float3
            {
                x = math.sin(spawnAngle),
                y = math.cos(spawnAngle),
                z = 0
            };

            // 根据生成距离调整坐标，并加上玩家位置偏移
            spawnPoint *= spawnData.SpawnDistance;
            spawnPoint += playerPosition;

            // 设置新敌人的位置
            ecb.SetComponent(newEnemy, LocalTransform.FromPosition(spawnPoint));
        }
    }
}