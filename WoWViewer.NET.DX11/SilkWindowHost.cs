using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;
using WoWRenderLib.DX11;

namespace WoWViewer.NET.DX11
{
    public class SilkWindowHost
    {
        private WowClientConfig _wowConfig;

        private WowViewerEngine wowViewerEngine;

        private IInputContext inputContext;

        private IWindow window;

        private bool hasFocus = true;

        // private SilkImGuiBackend silkImGuiBackend;

        // Load the DXGI and Direct3D11 libraries for later use.
        // Given this is not tied to the window, this doesn't need to be done in the OnLoad event.
        private DXGI dxgi = null!;
        private D3D11 d3d11 = null!;

        // These variables are initialized within the Load event.

        private ComPtr<ID3D11Device> device = default;
        private ComPtr<ID3D11DeviceContext> deviceContext = default;
        private ComPtr<IDXGIFactory2> factory = default;
        private ComPtr<IDXGISwapChain1> swapchain = default;



        public SilkWindowHost(WowClientConfig wowConfig)
        {
            _wowConfig = wowConfig;
        }

        public void Run()
        {
            var windowOptions = WindowOptions.Default;
            windowOptions.API = GraphicsAPI.None;
            windowOptions.Size = new Vector2D<int>(1920, 1080);
            windowOptions.Title = "WoWRenderLib.DX11";
            window = Window.Create(windowOptions);

#if DEBUG
            Evergine.Bindings.RenderDoc.RenderDoc.Load(out Evergine.Bindings.RenderDoc.RenderDoc renderDoc);
#endif

            window.Load += OnLoad;
            window.FramebufferResize += OnResize;
            window.FocusChanged += OnFocusChanged;
            window.Update += OnUpdate;

            window.Render += OnRender;
            window.Resize += OnResize;
            window.Closing += OnClose;

            // Starts main loop
            window.Run();
        }

        private void OnClose()
        {
            device.Dispose();
            deviceContext.Dispose();
            d3d11.Dispose();
            dxgi.Dispose();
        }

        private unsafe void OnLoad()
        {
            //Whether or not to force use of DXVK on platforms where native DirectX implementations are available
            const bool forceDxvk = false;

            dxgi = DXGI.GetApi(window, forceDxvk);
            d3d11 = D3D11.GetApi(window, forceDxvk);

            // Create our D3D11 logical device.
            SilkMarshal.ThrowHResult
            (
                d3d11.CreateDevice
                (
                    default(ComPtr<IDXGIAdapter>),
                    D3DDriverType.Hardware,
                    Software: default,
#if(DEBUG)
                    (uint)CreateDeviceFlag.Debug,
#else
                    0,
#endif
                    null,
                    0,
                    D3D11.SdkVersion,
                    ref device,
                    null,
                    ref deviceContext
                )
            );

#if (DEBUG)
            //This is not supported under DXVK 
            //TODO: PR a stub into DXVK for this maybe?
            if (OperatingSystem.IsWindows())
            {
                // Log debug messages for this device (given that we've enabled the debug flag). Don't do this in release code!
                device.SetInfoQueueCallback(msg => Console.WriteLine(SilkMarshal.PtrToString((nint)msg.PDescription)));
            }
#endif

            //gl = window.CreateOpenGL();

            inputContext = window.CreateInput();

            //   silkImGuiBackend = new SilkImGuiBackend(gl, window, inputContext);

            // var engine = new WowViewerEngine(_wowConfig, imgui);
            wowViewerEngine = new WowViewerEngine(_wowConfig, null, false);

            // Create our swapchain.
            var swapChainDesc = new SwapChainDesc1
            {
                BufferCount = 2, // double buffered
                Format = Format.FormatB8G8R8A8Unorm,
                BufferUsage = DXGI.UsageRenderTargetOutput,
                SwapEffect = SwapEffect.FlipDiscard,
                SampleDesc = new SampleDesc(1, 0)
            };

            // Create our DXGI factory to allow us to create a swapchain. 
            factory = dxgi.CreateDXGIFactory<IDXGIFactory2>();

            // Create the swapchain.
            SilkMarshal.ThrowHResult
            (
                factory.CreateSwapChainForHwnd
                (
                    device,
                    window.Native!.DXHandle!.Value,
                    in swapChainDesc,
                    null,
                    ref Unsafe.NullRef<IDXGIOutput>(),
                    ref swapchain
                )
            );

            wowViewerEngine.Initialize(dxgi, swapchain, device, deviceContext, window.FramebufferSize);
            wowViewerEngine.Resize((uint)window.FramebufferSize.X, (uint)window.FramebufferSize.Y);
        }

        private void OnResize(Vector2D<int> frameBufferSize)
        {
            wowViewerEngine.Resize((uint)frameBufferSize.X, (uint)frameBufferSize.Y);
        }

        private void OnUpdate(double deltaTime)
        {
            // silkImGuiBackend.Update((float)deltaTime);

            // Build inputs
            var primaryKeyboard = inputContext.Keyboards[0];
            var primaryMouse = inputContext.Mice[0];

            // build inputs for this frame
            InputFrame inputFrame = new InputFrame
            {
                MousePosition = primaryMouse.Position,
                LeftMouseDown = primaryMouse.IsButtonPressed(MouseButton.Left),
                RightMouseDown = primaryMouse.IsButtonPressed(MouseButton.Right),
                MouseWheel = primaryMouse.ScrollWheels[0].Y,
                KeysDown = new HashSet<Key>()
            };
            foreach (Key key in primaryKeyboard.SupportedKeys)
            {
                if (primaryKeyboard.IsKeyPressed(key))
                {
                    inputFrame.KeysDown.Add(key);
                }
            }

            wowViewerEngine.Update(deltaTime, inputFrame);
        }

        private unsafe void OnRender(double deltaTime)
        {
            if (!window.IsVisible || window.WindowState == WindowState.Minimized)
                return; // can cap fps instead

            wowViewerEngine.Render(deltaTime);
        }

        private void OnFocusChanged(bool focused)
        {
            hasFocus = focused;

            wowViewerEngine.SetHasFocus(focused);
        }

    }
}
