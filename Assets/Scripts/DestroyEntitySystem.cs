using System;
using TMG.Survivors;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct DestroyEntityFlag : IComponentData, IEnableableComponent
{
    
}

[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
[UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
public partial struct DestroyEntitySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var endEcbSystem = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var endEcb = endEcbSystem.CreateCommandBuffer(state.WorldUnmanaged);
        var beginEcbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var beginEcb = beginEcbSystem.CreateCommandBuffer(state.WorldUnmanaged);
        
        foreach (var (_, entity) in SystemAPI.Query<DestroyEntityFlag>().WithEntityAccess())
        {
            if (SystemAPI.HasComponent<PlayerTag>(entity))
            {
                GameUIController.Instance.ShowGameOverUI();
            }

            if (SystemAPI.HasComponent<GemPrefab>(entity))
            {
                var gemPrefab = SystemAPI.GetComponent<GemPrefab>(entity).Value;
                var newGem = beginEcb.Instantiate(gemPrefab);

                var spawnPosition = SystemAPI.GetComponent<LocalTransform>(entity).Position;
                beginEcb.SetComponent(newGem, LocalTransform.FromPosition(spawnPosition));
            }
            
            endEcb.DestroyEntity(entity);
        }
    }
}
