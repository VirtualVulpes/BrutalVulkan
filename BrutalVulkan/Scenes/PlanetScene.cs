using Brutal.Numerics;
using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Components;

namespace BrutalVulkan.Scenes;

public static class PlanetScene
{
    public static SceneDef Create() => new SceneDef("Planet Scene", [
        new EntityDef("main_camera",
            new TransformDef(
                new float3(3.0f, 0.0f, 5.0f),
                floatQuat.Identity,
                new float3(0.2f, 0.2f, 0.2f)),
            new MeshDef("cube"), new PipelineDef("Flat Color"), new ColorDef(new float3(0.5f, 0.5f, 0.5f)),
            new CameraDef((Single)Constants.WindowWidth / Constants.WindowHeight, Single.DegreesToRadians(60.0f), 0.1f,
                100.0f)),
        new PlanetDef("planet",
        [
            new TransformDef(
                new float3(0.0f, 0.0f, 0.0f),
                floatQuat.CreateFromAxisAngle(float3.UnitZ, Single.DegreesToRadians(23.44f)), // earth's axial tilt
                new float3(1.0f, 1.0f, 1.0f)),
            new RotateDef(new float3(0.0f, 0.15f, 0.0f), new float3(0.0f, 0.05f, 0.0f))
        ], [new TransformDef(float3.Zero, floatQuat.Identity, float3.One), new PipelineDef("Phong Color"), new ColorDef(new float3(0.1f, 0.2f, 0.1f))]),
        new EntityDef("directional_light",
            new TransformDef(
                new float3(-2.0f, 6.5f, -2.0f),
                floatQuat.Identity,
                new float3(0.05f, 0.05f, 0.05f)),
            new MeshDef("sphere"), new PipelineDef("Flat Color"), new ColorDef(new float3(1.0f, 0.95f, 0.85f)),
            new LightDef(new float3(1.0f, 0.95f, 0.85f), 0.4f, 0.0f, new float3(-1.0f, -1.0f, 0.0f),
                LightType.Directional, 0.0f, 0.0f)),
        new EntityDef("atmosphere",
            new TransformDef(
                new float3(0.0f, 0.0f, 0.0f),
                floatQuat.Identity,
                new float3(0.05f, 0.05f, 0.05f)),
            new PipelineDef("Atmosphere"))
    ]);
}