using System.Diagnostics;
using System.Runtime.InteropServices;
using Armature.DebugUi;
using Armature.Editor;
using Armature.Input;
using Armature.Rendering;
using Armature.Rendering.Passes;
using Armature.Rendering.Pipelines;
using Armature.Scene;
using Armature.Scene.Systems;
using Armature.SceneRendering;
using Armature.Vulkan;
using Brutal.GlfwApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using BrutalVulkan.Scenes;

namespace BrutalVulkan;

public sealed class App : IDisposable
{
    private readonly GlfwWindow _window;
    private GCHandle _gcHandle;

    private readonly InputManager _inputManager;

    private VulkanContext _context;
    private CoreDescriptors _coreDescriptors;

    private PipelineManager _pipelineManager;
    private TextureManager _textureManager;
    private MeshManager _meshManager;

    private Renderer _renderer;
    private SceneRenderer _sceneRenderer;

    private ImGuiManager _imGuiManager;
    private readonly Inspector _inspector;
    private readonly SceneView _sceneView;

    private CommandBufferManager _cmdManager;

    private readonly SystemManager _systemManager;
    private readonly CameraSystem _camera;
    private readonly CameraController _cameraController;

    private World _world;
    private SelectionHelper _selection;

    // Editor
    private EditorDescriptors _editorDescriptors;
    private EditorRenderFeature _editorFeatures;
    private EditorInput _editorInput;


    public App()
    {
        _window = Glfw.CreateWindow(
            new GlfwWindow.CreateInfo
                { Title = "Brutal Vulkan", Size = new int2(800, 600) }
        );
        _gcHandle = GCHandle.Alloc(this);
        unsafe
        {
            _window.UserPointer = GCHandle.ToIntPtr(_gcHandle).ToPointer();
        }

        _window.OnFramebufferSize += OnFramebufferSize;

        _inputManager = new InputManager(_window);

        _world = new World();

        _selection = new SelectionHelper(_world);
        _inspector = new Inspector(_world, _selection);
        _sceneView = new SceneView(_world, _selection, _inputManager);

        _systemManager = new SystemManager();
        _systemManager.AddSystem(new RenderSystem(_world));
        _systemManager.AddSystem(new RotateSystem(_world));
        _camera = _systemManager.AddSystem(new CameraSystem(_world));

        InitVulkan();


        var resolver = new AssetResolver(_meshManager, _pipelineManager, _textureManager);
        new SceneLoader(resolver).Load(DemoScene.Create(), _world);

        _editorInput =
            new EditorInput(_inputManager, _world, _editorFeatures.Picker,
                _selection);
        _cameraController = new CameraController(_world, _inputManager, _camera);
    }

    private static unsafe void OnFramebufferSize(GlfwWindow window, int2 size)
    {
        var handle = GCHandle.FromIntPtr((nint)window.UserPointer);
        var app = (App)handle.Target!;

        app._renderer.OnFramebufferResize();

        if (size.X == 0 || size.Y == 0) return;

        app._camera.SetAspectRatio((float)size.X / size.Y);
    }

    private void InitVulkan()
    {
        _context = new VulkanContext(_window);
        _coreDescriptors = new CoreDescriptors(_context);
        _editorDescriptors = new EditorDescriptors(_context);

        _pipelineManager = new PipelineManager(_context.Device);

        _cmdManager = new CommandBufferManager(_context.Device, _context.Queue, _context.CommandPool);

        _renderer = new Renderer(_context, _window, _coreDescriptors);

        var pipelineBuildContext = new PipelineBuildContext
        {
            ColorFormat = _renderer.Swapchain.SurfaceFormat.Format,
            MsaaSamples = _renderer.MsaaSamples
        };
        _imGuiManager =
            new ImGuiManager(_context.Allocator, _context.Device, _cmdManager, _window, pipelineBuildContext,
                _inputManager);
        CorePipelines.Register(_context.Device, _pipelineManager, pipelineBuildContext, _coreDescriptors);
        EditorPipelines.Register(_context.Device, _pipelineManager, pipelineBuildContext, _coreDescriptors,
            _editorDescriptors);

        _meshManager = new MeshManager(_context.Allocator);
        MeshLibrary.RegisterDefaults(_meshManager);

        LoadTextures();
        LoadModels();

        _sceneRenderer = new SceneRenderer(_pipelineManager, _meshManager, _context, _coreDescriptors);
        _renderer.AddListener(_sceneRenderer);

        _renderer.AddListener(_imGuiManager);

        _renderer.AddPass(new OpaquePass(), RenderPhase.Scene);

        _editorFeatures = new EditorRenderFeature(_renderer, _context, _pipelineManager, _editorDescriptors);

        _renderer.AddPass(new ImGuiPass(_imGuiManager), RenderPhase.Overlay);
    }

    public void Run()
    {
        _inputManager.SetInputMode(InputMode.CameraControl);

        _systemManager.Start();

        var last = Stopwatch.GetTimestamp();

        var renderSystem = _systemManager.GetSystem<RenderSystem>();

        while (!_window.ShouldClose)
        {
            var now = Stopwatch.GetTimestamp();
            var deltaTime = float.Min((float)Stopwatch.GetElapsedTime(last, now).TotalSeconds, 0.1f);
            last = now;

            _inputManager.Update();

            Glfw.PollEvents();

            _systemManager.Update(deltaTime);
            _cameraController.Update(deltaTime);

            if (_inputManager.IsKeyPressed(GlfwKey.Escape) && !_inputManager.IsUiCapturingKeyboard())
                _window.ShouldClose = true;

            _editorInput.Update();

            _imGuiManager.NewFrame(deltaTime);

            _inspector.Draw();
            _sceneView.Draw();

            var camera =
                _camera.GetActiveCameraData(new float2(_renderer.Swapchain.Extent.Width,
                    _renderer.Swapchain.Extent.Height));
            float4x4.Invert(camera.View, out var invView);
            float4x4.Invert(camera.Proj, out var invProj);
            var invViewport = new float2(1.0f / camera.ViewportSize.X, 1.0f / camera.ViewportSize.Y);
            var gpuCamera = new GpuCamera
            {
                View = camera.View,
                Proj = camera.Proj,
                ViewProj = camera.View * camera.Proj,
                InvView = invView,
                InvProj = invProj,
                Position = camera.Position,
                InvViewport = invViewport
            };
            renderSystem.BuildScene(gpuCamera);

            _sceneRenderer.Submit(renderSystem.Scene);
            _renderer.DrawFrame(_sceneRenderer.Items, _sceneRenderer.ViewProj);
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
            _coreDescriptors.UpdateTextureDescriptor(_textureManager.GetTexture(textureId));
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

        _imGuiManager.Dispose();

        _editorFeatures.Dispose();
        _editorDescriptors.Dispose();

        _sceneRenderer.Dispose();
        _renderer.Dispose();

        _pipelineManager.Dispose();
        _textureManager.Dispose();
        _meshManager.Dispose();
        _inputManager.Dispose();

        _coreDescriptors.Dispose();
        _context.Dispose();

        _window.OnFramebufferSize -= OnFramebufferSize;
        _window.Dispose();
        _gcHandle.Free();

        Glfw.Terminate();
    }
}