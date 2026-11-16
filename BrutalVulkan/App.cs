using System.Diagnostics;
using System.Runtime.InteropServices;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Systems;
using BrutalVulkan.Rendering;
using BrutalVulkan.Rendering.Meshes;
using BrutalVulkan.Rendering.Pipelines;
using BrutalVulkan.UI;

namespace BrutalVulkan;

public sealed class App : IDisposable
{
    private readonly GlfwWindow _window;
    private GCHandle _gcHandle;

    private readonly InputManager _inputManager;
    private readonly Camera _camera;

    private VulkanContext _context;
    private DescriptorManager _descriptorManager;
    
    private PipelineManager _pipelineManager;
    private TextureManager _textureManager;
    private MeshManager _meshManager;
    
    private Renderer _renderer;
    
    private ImGuiManager _imGuiManager;

    private CommandBufferManager _cmdManager;

    private World _world;
    private readonly SystemManager _systemManager;

    public App()
    {
        _window = Glfw.CreateWindow(
            new GlfwWindow.CreateInfo
                { Title = "Brutal Vulkan", Size = new int2(Constants.WindowWidth, Constants.WindowHeight) }
        );
        Glfw.PInvoke.SetInputMode(_window, (Int32)GlfwInputMode.Cursor, (Int32)GlfwCursorMode.Disabled);
        _gcHandle = GCHandle.Alloc(this);
        unsafe
        {
            _window.UserPointer = GCHandle.ToIntPtr(_gcHandle).ToPointer();
        }

        _window.OnFramebufferSize += OnFramebufferSize;

        _inputManager = new InputManager(_window);

        _camera = new Camera();

        InitVulkan();

        _world = new World();
        _systemManager = new SystemManager();
        _systemManager.AddSystem(new RenderSystem(_world));
        _systemManager.AddSystem(new RotateSystem(_world));

        var resolver = new AssetResolver(_meshManager, _pipelineManager, _textureManager);
        new SceneLoader(resolver).Load(DemoScene.Create(), _world);
    }

    private static unsafe void OnFramebufferSize(GlfwWindow window, int2 size)
    {
        var handle = GCHandle.FromIntPtr((nint)window.UserPointer);
        var app = (App)handle.Target!;

        app._renderer.OnFramebufferResize();
        app._camera.AspectRatio = (Single)size.X / size.Y;
    }

    private void InitVulkan()
    {
        _context = new VulkanContext(_window);
        _descriptorManager = new DescriptorManager(_context);

        _pipelineManager = new PipelineManager(_context.Device);

        _cmdManager = new CommandBufferManager(_context.Device, _context.Queue, _context.CommandPool);

        _renderer = new Renderer(_context.Device, _context.PhysicalDevice, _context.Surface, _window, _context.Allocator, _context.CommandPool, _context.Queue);

        var pipelineBuildContext = new PipelineBuildContext
        {
            ColorFormat = _renderer.SwapchainFormat.Format,
            MsaaSamples = _renderer.MsaaSamples
        };
        _imGuiManager = new ImGuiManager(_context.Allocator, _context.Device, _cmdManager, _window, pipelineBuildContext);
        var pipelineFactory = new PipelineFactory(_context.Device, _pipelineManager, pipelineBuildContext, _descriptorManager.DescriptorSetLayout);
        pipelineFactory.CreateDefaultPipelines();

        _meshManager = new MeshManager(_context.Allocator);
        MeshLibrary.RegisterDefaults(_meshManager);
        
        LoadTextures();
        LoadModels();

        _renderer.Initialize(_pipelineManager, _descriptorManager.DescriptorSets, _imGuiManager, _meshManager);
    }

    public void Run()
    {
        var last = Stopwatch.GetTimestamp();

        var renderSystem = _systemManager.GetSystem<RenderSystem>();

        while (!_window.ShouldClose)
        {
            var now = Stopwatch.GetTimestamp();
            var dt = Single.Min((Single)Stopwatch.GetElapsedTime(last, now).TotalSeconds, 0.1f);
            last = now;

            _inputManager.Update();

            Glfw.PollEvents();

            _systemManager.Update(dt);

            if (_inputManager.IsKeyPressed(GlfwKey.Escape))
                _window.ShouldClose = true;

            switch (_inputManager.InputMode)
            {
                case InputMode.CameraControl when _inputManager.IsKeyPressed(GlfwKey.Tab):
                    _inputManager.SetInputMode(InputMode.UiInteraction);
                    break;
                case InputMode.CameraControl:
                {
                    // camera movement
                    var movement = float3.Zero;
                    var speed = 5.0f;
                    if (_inputManager.IsKeyDown(GlfwKey.W))
                        movement += float3.UnitZ;
                    if (_inputManager.IsKeyDown(GlfwKey.S))
                        movement -= float3.UnitZ;
                    if (_inputManager.IsKeyDown(GlfwKey.A))
                        movement -= float3.UnitX;
                    if (_inputManager.IsKeyDown(GlfwKey.D))
                        movement += float3.UnitX;
                    if (_inputManager.IsKeyDown(GlfwKey.Space))
                        movement += float3.UnitY;
                    if (_inputManager.IsKeyDown(GlfwKey.LeftControl))
                        movement -= float3.UnitY;
                    _camera.MoveRelative(float3.NormalizeOrZero(movement) * dt * speed);

                    // camera rotation
                    var mouseDelta = _inputManager.MouseDelta;
                    var sensitivity = 0.001f;
                    _camera.Rotate((Single)mouseDelta.X * sensitivity, -(Single)mouseDelta.Y * sensitivity);

                    break;
                }
                case InputMode.UiInteraction:
                {
                    if (_inputManager.IsMouseButtonPressed(GlfwMouseButton.Left) &&
                        !_inputManager.IsImGuiCapturingMouse())
                    {
                        _inputManager.SetInputMode(InputMode.CameraControl);
                    }

                    break;
                }
            }

            _imGuiManager.NewFrame(dt);

            CreateUI();

            _renderer.DrawFrame(renderSystem.BuildRenderObjects(), _camera, renderSystem.BuildGpuLights());
        }
    }

    private void CreateUI()
    {
        ImGui.Begin("Hello ImGui"u8);
        ImGui.Text("Hello, world!"u8);
        ImGui.End();
    }
    
    private void LoadTextures()
    {
        _textureManager = new TextureManager(_context.PhysicalDevice.GetFeatures().TextureCompressionBC);

        var commandBuffer = _cmdManager.BeginSingleTimeCommands();

        foreach (var fileName in new[]
                 {
                     "brick.ktx2",
                     "statue.ktx2",
                     "viking_room.ktx2"
                 })
        {
            var textureId = _textureManager.AddTexture(fileName, commandBuffer, _context.Allocator, TextureColorSpace.SRgb);
            _descriptorManager.UpdateTextureDescriptor(_textureManager.GetTexture(textureId));
        }

        _cmdManager.EndSingleTimeCommands(ref commandBuffer);
        _textureManager.ClearStagingBuffers();
    }

    private void LoadModels()
    {
        var mesh = ModelLoader.LoadModel("viking_room.gltf");
        _meshManager.AddMesh("viking_room", mesh);
    }

    public void Dispose()
    {
        _context.Device.WaitIdle();

        _renderer.Dispose();

        _imGuiManager.Dispose();
        
        _pipelineManager.Dispose();
        _textureManager.Dispose();
        _meshManager.Dispose();
        _inputManager.Dispose();
        
        _descriptorManager.Dispose();
        _context.Dispose();

        _window.OnFramebufferSize -= OnFramebufferSize;
        _window.Dispose();
        _gcHandle.Free();

        Glfw.Terminate();
    }
}