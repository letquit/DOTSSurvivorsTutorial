using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public struct InitializeCharacterFlag : IComponentData, IEnableableComponent
{
    
}

public struct CharacterMoveDirection : IComponentData
{
    public float2 Value;
}

public struct CharacterMoveSpeed : IComponentData
{
    public float Value;
}

[MaterialProperty("_FacingDirection")]
public struct FacingDirectionOverride : IComponentData
{
    public float Value;
}

public class CharacterAuthoring : MonoBehaviour
{
    public float MoveSpeed;
    
    private class Baker : Baker<CharacterAuthoring>
    {
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
        }
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct CharacterInitializationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (mass, shouldInitialize) in SystemAPI.Query<RefRW<PhysicsMass>, EnabledRefRW<InitializeCharacterFlag>>())
        {
            mass.ValueRW.InverseInertia = float3.zero;
            shouldInitialize.ValueRW = false;
        }
    }
}

public partial struct CharacterMoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (velocity, facingDirection, direction, speed, entity) in SystemAPI.Query<RefRW<PhysicsVelocity>, RefRW<FacingDirectionOverride>, CharacterMoveDirection, CharacterMoveSpeed>().WithEntityAccess())
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
                var animationType = math.lengthsq(moveStep2d) > float.Epsilon ? PlayerAnimationIndex.Movement : PlayerAnimationIndex.Idle;
                animationOverride.ValueRW.Value = (float)animationType;
            }
        }
    }
}

public partial struct GlobalTimeUpdateSystem : ISystem
{
    private static int _globalTimeShaderPropertyID;

    public void OnCreate(ref SystemState state)
    {
        _globalTimeShaderPropertyID = Shader.PropertyToID("_GlobalTime");
    }

    public void OnUpdate(ref SystemState state)
    {
        Shader.SetGlobalFloat(_globalTimeShaderPropertyID, (float)SystemAPI.Time.ElapsedTime);
    }
}
