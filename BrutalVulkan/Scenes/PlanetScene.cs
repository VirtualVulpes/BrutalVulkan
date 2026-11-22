using Brutal.Numerics;
using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Components;

namespace BrutalVulkan.Scenes;

public static class PlanetScene
{
    public static SceneDef Create() => new(Name: "Planet Scene",
        Definitions:
        [
            new EntityDef(Name: "main_camera",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(0.0f, 0.0f, 5.0f),
                        Scale = new float3(0.2f, 0.2f, 0.2f)
                    },
                    new MeshDef("cube"), 
                    new PipelineDef("Flat Color"),
                    new CameraDef()
                ]),
            new PlanetDef(Name: "planet",
                Components:
                [
                    new TransformDef
                    {
                        Rotation = floatQuat.CreateFromAxisAngle(float3.UnitZ,
                            Single.DegreesToRadians(23.44f)), // earth's axial tilt
                    },
                    new RotateDef
                    {
                        LocalAngularVelocity = new float3(0.0f, 0.15f, 0.0f),
                        WorldAngularVelocity = new float3(0.0f, 0.05f, 0.0f)
                    }
                ],
                FaceComponents:
                [
                    TransformDef.Identity(), 
                    new PipelineDef("Phong Color"),
                    new ColorDef(new float3(0.1f, 0.2f, 0.1f))
                ],
                Children:
                [
                    new EntityDef(Name: "atmosphere",
                        Components:
                        [
                            TransformDef.Identity(),
                            new PipelineDef("Atmosphere"), 
                            new AtmosphereDef(1.05f)
                        ])
                ]),
            new EntityDef(Name: "directional_light",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(-2.0f, 6.5f, -2.0f),
                        Scale = new float3(0.05f, 0.05f, 0.05f)
                    },
                    new MeshDef("sphere"), 
                    new PipelineDef("Flat Color"), 
                    new ColorDef(new float3(1.0f, 0.95f, 0.85f)),
                    new LightDef
                    {
                        Type = LightType.Directional,
                        Color = new float3(1.0f, 0.95f, 0.85f),
                        Intensity = 0.4f,
                        Direction = new float3(-1.0f, -1.0f, 0.0f)
                    }
                ])
        ]);
}