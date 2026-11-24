using Armature.Scene;
using Armature.Scene.Components;
using Brutal.Numerics;

namespace BrutalVulkan.Scenes;

public static class DemoScene
{
    public static SceneDef Create() => new((string)"Demo Scene",
        Definitions:
        [
            new EntityDef(Name: "Floor",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(0.0f, -2.5f, 0.0f),
                        Rotation = floatQuat.CreateFromAxisAngle(float3.UnitX, Single.DegreesToRadians(-90.0f)),
                        Scale = new float3(10.0f, 10.0f, 1.0f)
                    },
                    new MeshDef("plane"),
                    new PipelineDef("Phong Texture"),
                    new TextureDef("brick")
                ]),
            new EntityDef(Name: "Spinning Cube",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(0.0f, 5.0f, -1.0f),
                    },
                    new MeshDef("cube"),
                    new PipelineDef("Phong Texture"),
                    new TextureDef("statue"),
                    new RotateDef
                    {
                        LocalAngularVelocity = new float3(1.0f, 0.2f, 0.7f),
                        WorldAngularVelocity = new float3(0.2f, 1.0f, 0.4f)
                    }
                ]),
            new EntityDef(Name: "Spinning Cube",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(3.0f, 5.0f, -1.0f),
                    },
                    new MeshDef("cube"),
                    new PipelineDef("Phong Texture"),
                    new TextureDef("statue"),
                    new RotateDef
                    {
                        LocalAngularVelocity = new float3(1.0f, 0.2f, 0.7f),
                        WorldAngularVelocity = new float3(0.2f, 1.0f, 0.4f)
                    }
                ]),
            new EntityDef(Name: "Viking Room",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(0.0f, -2.0f, -8.0f),
                        Rotation = floatQuat.CreateFromAxisAngle(float3.UnitY, Single.DegreesToRadians(-90f)),
                        Scale = new float3(5.0f, 5.0f, 5.0f)
                    },
                    new MeshDef("viking_room"),
                    new PipelineDef("Phong Texture"),
                    new TextureDef("viking_room")
                ]),
            new EntityDef(Name: "Textured Sphere",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(6.0f, 1.0f, 0.0f),
                    },
                    new MeshDef("sphere"),
                    new PipelineDef("Phong Texture"),
                    new TextureDef("statue")
                ]),
            new EntityDef(Name: "Wireframe Sphere",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(-6.0f, 1.0f, 0.0f),
                        Rotation = floatQuat.CreateFromAxisAngle(float3.UnitZ,
                            Single.DegreesToRadians(23.44f)) // earth's axial tilt
                    },
                    new MeshDef("sphere"),
                    new PipelineDef("Wireframe"),
                    new ColorDef(new float3(0.6f, 0.3f, 0.6f)),
                    new RotateDef
                    {
                        LocalAngularVelocity = new float3(0.0f, 0.15f, 0.0f),
                        WorldAngularVelocity = new float3(0.0f, 0.05f, 0.0f)
                    }
                ],
                Children:
                [
                    new EntityDef(Name: "Wireframe Sphere 2",
                        Components:
                        [
                            new TransformDef
                            {
                                Position = new float3(3.0f, 0.0f, 0.0f),
                                Scale = new float3(0.1f, 0.1f, 0.1f)
                            },
                            new MeshDef("sphere_low_res"),
                            new PipelineDef("Wireframe"),
                            new ColorDef(new float3(0.6f, 0.3f, 0.6f)),
                            new RotateDef
                            {
                                LocalAngularVelocity = new float3(0.0f, 1.0f, 0.0f),
                            }
                        ])
                ]),
            new EntityDef(Name: "Directional Light",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(-2.0f, 6.5f, -2.0f),
                        Scale = new float3(0.05f, 0.05f, 0.05f)
                    },
                    new MeshDef("sphere_low_res"),
                    new PipelineDef("Flat Color"),
                    new ColorDef(new float3(1.0f, 0.95f, 0.85f)),
                    new LightDef
                    {
                        Type = LightType.Directional,
                        Color = new float3(1.0f, 0.95f, 0.85f),
                        Intensity = 0.4f,
                        Direction = new float3(-1.0f, -1.0f, 0.0f)
                    }
                ]),
            new EntityDef(Name: "Point Light",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(1.0f, -1.0f, -5.5f),
                        Scale = new float3(0.05f, 0.05f, 0.05f)
                    },
                    new MeshDef("sphere_low_res"),
                    new PipelineDef("Flat Color"),
                    new ColorDef(new float3(0.3f, 0.2f, 0.6f)),
                    new LightDef
                    {
                        Type = LightType.Point,
                        Color = new float3(0.3f, 0.2f, 0.6f),
                        Intensity = 10.0f,
                        Range = 10.0f
                    }
                ]),
            new EntityDef(Name: "Spot Light",
                Components:
                [
                    new TransformDef
                    {
                        Scale = new float3(0.05f, 0.05f, 0.05f)
                    },
                    new MeshDef("sphere_low_res"),
                    new PipelineDef("Flat Color"),
                    new ColorDef(new float3(1.0f, 0.5f, 0.5f)),
                    new LightDef
                    {
                        Type = LightType.Spot,
                        Color = new float3(1.0f, 0.5f, 0.5f),
                        Intensity = 10.0f,
                        Range = 40.0f,
                        Direction = new float3(0.2f, -1.0f, 0.4f),
                        InnerCos = Single.Cos(Single.DegreesToRadians(40.0f)),
                        OuterCos = Single.Cos(Single.DegreesToRadians(50.0f))
                    }
                ]),
            new EntityDef(Name: "Main Camera",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(0.0f, 1.0f, 8.0f),
                        Scale = new float3(0.2f, 0.2f, 0.2f)
                    },
                    new MeshDef("cube"),
                    new PipelineDef("Flat Color"),
                    new ColorDef(new float3(0.5f, 0.5f, 0.5f)),
                    new CameraDef()
                ]),
            new EntityDef(Name: "Left Camera",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(-6.0f, 6.0f, 10.0f),
                        Rotation = floatQuat.CreateFromAxisAngle(float3.UnitY, Single.DegreesToRadians(-30.0f)) *
                                   floatQuat.CreateFromAxisAngle(float3.UnitX, Single.DegreesToRadians(-30.0f)),
                        Scale = new float3(0.2f, 0.2f, 0.2f)
                    },
                    new MeshDef("cube"),
                    new PipelineDef("Flat Color"),
                    new ColorDef(new float3(0.5f, 0.5f, 0.5f)),
                    new CameraDef()
                ]),
            new EntityDef(Name: "Right Camera",
                Components:
                [
                    new TransformDef
                    {
                        Position = new float3(6.0f, 6.0f, 10.0f),
                        Rotation = floatQuat.CreateFromAxisAngle(float3.UnitY, Single.DegreesToRadians(30.0f)) *
                                   floatQuat.CreateFromAxisAngle(float3.UnitX, Single.DegreesToRadians(-30.0f)),
                        Scale = new float3(0.2f, 0.2f, 0.2f)
                    },
                    new MeshDef("cube"),
                    new PipelineDef("Flat Color"),
                    new ColorDef(new float3(0.5f, 0.5f, 0.5f)),
                    new CameraDef()
                ])
        ]);
}