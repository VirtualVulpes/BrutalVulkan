using Brutal.Numerics;
using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Components;

namespace BrutalVulkan.Scenes;

public static class PlanetScene
{
    public static SceneDef Create() => new SceneDef("Planet Scene", [
        new EntityDef("main_camera",
            new TransformDef(
                new float3(4.0f, 0.0f, 8.0f),
                floatQuat.Identity,
                new float3(0.2f, 0.2f, 0.2f)),
            new MeshDef("cube"), new PipelineDef("Flat Color"), new ColorDef(new float3(0.5f, 0.5f, 0.5f)),
            new CameraDef((Single)Constants.WindowWidth / Constants.WindowHeight, Single.DegreesToRadians(60.0f), 0.1f,
                100.0f)),
        new EntityDef("planet",
            new TransformDef(
                new float3(0.0f, 0.0f, 0.0f),
                floatQuat.Identity,
                new float3(1.0f, 1.0f, 1.0f)),
            new MeshDef("sphere"), new PipelineDef("Wireframe"), new ColorDef(new float3(0.5f, 0.5f, 0.5f))),
        new EntityDef("node",
            new TransformDef(
                new float3(3.0f, 0.0f, 0.0f),
                floatQuat.Identity,
                new float3(1.0f, 1.0f, 1.0f)),
            new NodeDef(Face.Front, 0, 0, 0), new PipelineDef("Wireframe"), new ColorDef(new float3(0.5f, 0.5f, 0.5f)))
    ]);
}