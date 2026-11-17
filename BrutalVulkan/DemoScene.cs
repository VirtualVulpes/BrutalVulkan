using Brutal.GltfApi;
using Brutal.Numerics;
using BrutalVulkan.ECS;

namespace BrutalVulkan;

public static class DemoScene
{
    public static SceneDef Create() => new("Demo", [
        new EntityDef("floor",
            new TransformDef(
                new float3(0.0f, -2.5f, 0.0f),
                floatQuat.CreateFromAxisAngle(float3.UnitX, Single.DegreesToRadians(-90.0f)),
                new float3(10.0f, 10.0f, 1.0f)),
            new MeshDef("plane"), new PipelineDef("Phong Texture"), new TextureDef("brick")),
        new EntityDef("spinning_cube",
            new TransformDef(
                new float3(0.0f, 5.0f, -1.0f),
                floatQuat.Identity,
                new float3(1.0f, 1.0f, 1.0f)),
            new MeshDef("cube"), new PipelineDef("Phong Texture"), new TextureDef("statue"),
            new RotateDef(new float3(1.0f, 0.2f, 0.7f), new float3(0.2f, 1.0f, 0.4f)),
            new SelectionDef()),
        new EntityDef("viking_room",
            new TransformDef(
                new float3(0.0f, -2.0f, -8.0f),
                floatQuat.CreateFromAxisAngle(float3.UnitY, Single.DegreesToRadians(-90f)),
                new float3(5.0f, 5.0f, 5.0f)),
            new MeshDef("viking_room"), new PipelineDef("Phong Texture"), new TextureDef("viking_room"),
            new SelectionDef()),
        new EntityDef("textured_planet",
            new TransformDef(
                new float3(6.0f, 1.0f, 0.0f),
                floatQuat.Identity,
                new float3(1.0f, 1.0f, 1.0f)),
            new MeshDef("planet"), new PipelineDef("Phong Texture"), new TextureDef("statue")),
        new EntityDef("wireframe_planet",
            new TransformDef(
                new float3(-6.0f, 1.0f, 0.0f),
                floatQuat.CreateFromAxisAngle(float3.UnitZ, Single.DegreesToRadians(23.44f)), // earth's axial tilt
                new float3(1.0f, 1.0f, 1.0f)),
            new MeshDef("planet"), new PipelineDef("Wireframe"), new ColorDef(new float3(0.6f, 0.3f, 0.6f)),
            new RotateDef(new float3(0.0f, 0.15f, 0.0f), new float3(0.0f, 0.05f, 0.0f))),
        new EntityDef("main_light",
            new TransformDef(
                new float3(-2.0f, 6.5f, -2.0f),
                floatQuat.Identity,
                new float3(0.05f, 0.05f, 0.05f)),
            new MeshDef("sphere"), new PipelineDef("Flat Color"), new ColorDef(new float3(1.0f, 0.95f, 0.85f)),
            new LightDef(new float3(1.0f, 0.95f, 0.85f), 1.0f)),
        new EntityDef("back_light",
            new TransformDef(
                new float3(3.0f, 0.5f, 5.0f),
                floatQuat.Identity,
                new float3(0.05f, 0.05f, 0.05f)),
            new MeshDef("sphere"), new PipelineDef("Flat Color"), new ColorDef(new float3(0.2f, 0.2f, 0.4f)),
            new LightDef(new float3(0.2f, 0.2f, 0.4f), 0.4f)),
        new EntityDef("main_camera",
            new TransformDef(
                new float3(0.0f, 1.0f, 8.0f),
                floatQuat.Identity,
                new float3(0.2f, 0.2f, 0.2f)),
            new MeshDef("cube"), new PipelineDef("Flat Color"), new ColorDef(new float3(0.5f, 0.5f, 0.5f)),
            new CameraDef((Single)Constants.WindowWidth / Constants.WindowHeight, Single.DegreesToRadians(60.0f), 0.1f,
                100.0f, true)),
        new EntityDef("left_camera",
            new TransformDef(
                new float3(-6.0f, 6.0f, 10.0f),
                floatQuat.CreateFromAxisAngle(float3.UnitY, Single.DegreesToRadians(-30.0f)) *
                floatQuat.CreateFromAxisAngle(float3.UnitX, Single.DegreesToRadians(-30.0f)),
                new float3(0.2f, 0.2f, 0.2f)),
            new MeshDef("cube"), new PipelineDef("Flat Color"), new ColorDef(new float3(0.5f, 0.5f, 0.5f)),
            new CameraDef((Single)Constants.WindowWidth / Constants.WindowHeight, Single.DegreesToRadians(60.0f), 0.1f,
                100.0f, false)),
        new EntityDef("right_camera",
            new TransformDef(
                new float3(6.0f, 6.0f, 10.0f),
                floatQuat.CreateFromAxisAngle(float3.UnitY, Single.DegreesToRadians(30.0f)) *
                floatQuat.CreateFromAxisAngle(float3.UnitX, Single.DegreesToRadians(-30.0f)),
                new float3(0.2f, 0.2f, 0.2f)),
            new MeshDef("cube"), new PipelineDef("Flat Color"), new ColorDef(new float3(0.5f, 0.5f, 0.5f)),
            new CameraDef((Single)Constants.WindowWidth / Constants.WindowHeight, Single.DegreesToRadians(60.0f), 0.1f,
                100.0f, false)),
    ]);
}