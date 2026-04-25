using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;
using System.Text;
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
        private ComPtr<ID3D11DeviceContext1> deviceContext1 = default;
        private ComPtr<IDXGIFactory2> factory = default;
        private ComPtr<IDXGISwapChain1> swapchain = default;


        private ComPtr<ID3D11RenderTargetView> backbufferRTV = default;
        private ComPtr<ID3D11VertexShader> vertexShader = default;
        private ComPtr<ID3D11PixelShader> pixelShader = default;
        private ComPtr<ID3D11SamplerState> sampler = default;

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
            vertexShader.Dispose();
            pixelShader.Dispose();
            sampler.Dispose();
            backbufferRTV.Dispose();
            wowViewerEngine.Dispose();
            swapchain.Dispose();
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

            deviceContext1 = deviceContext.QueryInterface<ID3D11DeviceContext1>();

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

            wowViewerEngine.Initialize(dxgi, device, deviceContext, window.FramebufferSize);
            CreateBackbufferRTV();
            CreateShaders();
            CreateSampler();
        }

        private unsafe void CreateBackbufferRTV()
        {
            if (backbufferRTV.Handle != null) backbufferRTV.Dispose();

            ComPtr<ID3D11Texture2D> backbuffer = default;
            swapchain.GetBuffer(0, out backbuffer);
            device.CreateRenderTargetView(backbuffer, null, ref backbufferRTV);
            backbuffer.Dispose();
        }

        private unsafe void CreateShaders()
        {
            const string hlsl = @"
                Texture2D tex : register(t0);
                SamplerState samp : register(s0);
                struct VS_OUT { float4 pos : SV_Position; float2 uv : TEXCOORD0; };
                VS_OUT VS_Main(uint id : SV_VertexID) {
                    VS_OUT o;
                    o.uv  = float2((id & 1) * 2.0, (id >> 1) * 2.0);
                    o.pos = float4(o.uv * float2(2,-2) + float2(-1,1), 0, 1);
                    return o;
                }
                float4 PS_Main(VS_OUT i) : SV_Target { return tex.Sample(samp, i.uv); }
            ";

            var compiler = D3DCompiler.GetApi();
            var shaderBytes = Encoding.ASCII.GetBytes(hlsl);

            ComPtr<ID3D10Blob> vertexCode = default;
            ComPtr<ID3D10Blob> vertexErrors = default;
            HResult hr = compiler.Compile(
                in shaderBytes[0],
                (nuint)shaderBytes.Length,
                nameof(hlsl),
                null,
                ref Unsafe.NullRef<ID3DInclude>(),
                "VS_Main",
                "vs_5_0",
                0, 0,
                ref vertexCode,
                ref vertexErrors
            );

            if (hr.IsFailure)
            {
                if (vertexErrors.Handle is not null)
                    Console.WriteLine(SilkMarshal.PtrToString((nint)vertexErrors.GetBufferPointer()));
                hr.Throw();
            }

            ComPtr<ID3D10Blob> pixelCode = default;
            ComPtr<ID3D10Blob> pixelErrors = default;
            hr = compiler.Compile(
                in shaderBytes[0],
                (nuint)shaderBytes.Length,
                nameof(hlsl),
                null,
                ref Unsafe.NullRef<ID3DInclude>(),
                "PS_Main",
                "ps_5_0",
                0, 0,
                ref pixelCode,
                ref pixelErrors
            );

            if (hr.IsFailure)
            {
                if (pixelErrors.Handle is not null)
                    Console.WriteLine(SilkMarshal.PtrToString((nint)pixelErrors.GetBufferPointer()));
                hr.Throw();
            }

            SilkMarshal.ThrowHResult(device.CreateVertexShader(
                vertexCode.GetBufferPointer(),
                vertexCode.GetBufferSize(),
                ref Unsafe.NullRef<ID3D11ClassLinkage>(),
                ref vertexShader
            ));

            SilkMarshal.ThrowHResult(device.CreatePixelShader(
                pixelCode.GetBufferPointer(),
                pixelCode.GetBufferSize(),
                ref Unsafe.NullRef<ID3D11ClassLinkage>(),
                ref pixelShader
            ));

            vertexCode.Dispose();
            vertexErrors.Dispose();
            pixelCode.Dispose();
            pixelErrors.Dispose();
        }

        private void CreateSampler()
        {
            var sampDesc = new SamplerDesc
            {
                Filter = Filter.MinMagMipPoint,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
            };
            device.CreateSamplerState(in sampDesc, ref sampler);
        }

        private unsafe void OnResize(Vector2D<int> frameBufferSize)
        {
            if (frameBufferSize.X == 0 || frameBufferSize.Y == 0)
                return;

            deviceContext.ClearState();
            deviceContext.Flush();

            if (backbufferRTV.Handle != null) { backbufferRTV.Dispose(); backbufferRTV = default; }

            swapchain.ResizeBuffers(0, (uint)frameBufferSize.X, (uint)frameBufferSize.Y, Format.FormatUnknown, 0);

            CreateBackbufferRTV();
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

            var srv = wowViewerEngine.SharedSRV;

            var viewport = new Viewport(0, 0, viewportWidth, viewportHeight, 0f, 1f);
            deviceContext.RSSetViewports(1, in viewport);
            deviceContext1.OMSetRenderTargets(1, ref backbufferRTV, ref Unsafe.NullRef<ID3D11DepthStencilView>());
            deviceContext.VSSetShader(vertexShader, null, 0);
            deviceContext.PSSetShader(pixelShader, null, 0);
            deviceContext.PSSetShaderResources(0, 1, ref srv);
            deviceContext.PSSetSamplers(0, 1, ref sampler);
            deviceContext.IASetPrimitiveTopology(D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);
            deviceContext.Draw(3, 0);

            swapchain.Present(1, 0);
        }

        private int viewportWidth => wowViewerEngine != null ? (int)window.FramebufferSize.X : 1;
        private int viewportHeight => wowViewerEngine != null ? (int)window.FramebufferSize.Y : 1;

        private void OnFocusChanged(bool focused)
        {
            hasFocus = focused;

            wowViewerEngine.SetHasFocus(focused);
        }

    }
}
