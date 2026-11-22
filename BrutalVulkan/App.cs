using System.Diagnostics;
using System.Runtime.InteropServices;
using Brutal.GlfwApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using BrutalVulkan.Camera;
using BrutalVulkan.ECS;
using BrutalVulkan.ECS.Systems;
using BrutalVulkan.Input;
using BrutalVulkan.Rendering;
using BrutalVulkan.Rendering.Meshes;
using BrutalVulkan.Rendering.Pipelines;
using BrutalVulkan.Scenes;
using BrutalVulkan.UI;

namespace BrutalVulkan;

public sealed class App : IDisposable
{
    private readonly GlfwWindow _window;
    private GCHandle _gcHandle;

    private readonly InputManager _inputManager;

    private VulkanContext _context;
    private DescriptorManager _descriptorManager;

    private PipelineManager _pipelineManager;
    private TextureManager _textureManager;
    private MeshManager _meshManager;

    private Renderer _renderer;

    private ImGuiManager _imGuiManager;
    private readonly Inspector _inspector;
    private readonly SceneView _sceneView;

    private CommandBufferManager _cmdManager;

    private readonly SystemManager _systemManager;
    private readonly CameraSystem _cameraSystem;
    private readonly CameraController _cameraController;

    private EntityPicker _picker;
    private readonly EditorInput _editorInput;

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

        InitVulkan();

        var world = new World();
        var selection = new SelectionHelper(world, _inputManager);
        _inspector = new Inspector(world, selection);
        _sceneView = new SceneView(world, selection);

        _systemManager = new SystemManager();
        var transformSystem = _systemManager.AddSystem(new TransformSystem(world));
        _systemManager.AddSystem(new RenderSystem(world, transformSystem));
        _systemManager.AddSystem(new RotateSystem(world));
        _cameraSystem = _systemManager.AddSystem(new CameraSystem(world));
        _systemManager.AddSystem(new NodeSystem(world, _meshManager));

        _editorInput = new EditorInput(_inputManager, world, _systemManager.GetSystem<NodeSystem>(), _picker, selection);

        var resolver = new AssetResolver(_meshManager, _pipelineManager, _textureManager);
        new SceneLoader(resolver).Load(DemoScene.Create(), world);

        _cameraController = new CameraController(world, _inputManager, _cameraSystem);
    }

    private static unsafe void OnFramebufferSize(GlfwWindow window, int2 size)
    {
        var handle = GCHandle.FromIntPtr((nint)window.UserPointer);
        var app = (App)handle.Target!;

        app._renderer.OnFramebufferResize();

        if (size.X == 0 || size.Y == 0) return;

        app._cameraSystem.SetAspectRatio((Single)size.X / size.Y);
    }

    private void InitVulkan()
    {
        _context = new VulkanContext(_window);
        _descriptorManager = new DescriptorManager(_context);

        _pipelineManager = new PipelineManager(_context.Device);

        _cmdManager = new CommandBufferManager(_context.Device, _context.Queue, _context.CommandPool);

        _renderer = new Renderer(_context, _window);

        var pipelineBuildContext = new PipelineBuildContext
        {
            ColorFormat = _renderer.Swapchain.SurfaceFormat.Format,
            MsaaSamples = _renderer.MsaaSamples
        };
        _imGuiManager =
            new ImGuiManager(_context.Allocator, _context.Device, _cmdManager, _window, pipelineBuildContext, _inputManager);
        var pipelineFactory =
            new PipelineFactory(_context.Device, _pipelineManager, pipelineBuildContext, _descriptorManager);
        pipelineFactory.CreateDefaultPipelines();

        _meshManager = new MeshManager(_context.Allocator);
        MeshLibrary.RegisterDefaults(_meshManager);

        LoadTextures();
        LoadModels();

        _picker = new EntityPicker(_context.Device, _inputManager);
        _renderer.Initialize(_pipelineManager, _descriptorManager, _imGuiManager, _meshManager, _picker);
    }

    public void Run()
    {
        _systemManager.Start();

        var last = Stopwatch.GetTimestamp();

        var renderSystem = _systemManager.GetSystem<RenderSystem>();

        while (!_window.ShouldClose)
        {
            var now = Stopwatch.GetTimestamp();
            var deltaTime = Single.Min((Single)Stopwatch.GetElapsedTime(last, now).TotalSeconds, 0.1f);
            last = now;

            _inputManager.Update();

            Glfw.PollEvents();

            _systemManager.Update(deltaTime);
            _cameraController.Update(deltaTime);
            
            if (_inputManager.IsKeyPressed(GlfwKey.Escape) && !_inputManager.IsImGuiCapturingKeyboard())
                _window.ShouldClose = true;

            _editorInput.Update();

            _imGuiManager.NewFrame(deltaTime);

            _inspector.Draw();
            _sceneView.Draw();

            renderSystem.BuildScene(_cameraSystem.GetActiveCameraData(_renderer.Swapchain.Extent));
            _renderer.DrawFrame(renderSystem.Scene);
        }
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
            var textureId =
                _textureManager.AddTexture(fileName, commandBuffer, _context.Allocator, TextureColorSpace.SRgb);
            _descriptorManager.UpdateTextureDescriptor(_textureManager.GetTexture(textureId));
        }

        _cmdManager.EndSingleTimeCommands(commandBuffer);
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
        _picker.Dispose();

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