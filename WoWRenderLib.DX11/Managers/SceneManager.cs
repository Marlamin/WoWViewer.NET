using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WoWFormatLib.Structs.WDT;
using WoWRenderLib.DX11.Cache;
using WoWRenderLib.DX11.Loaders;
using WoWRenderLib.DX11.Objects;
using WoWRenderLib.DX11.Raycasting;
using WoWRenderLib.DX11.Renderer;
using WoWRenderLib.DX11.Structs;

namespace WoWRenderLib.DX11.Managers
{
    public class SceneManager(ComPtr<ID3D11Device> device, ComPtr<IDXGISwapChain1> swapchain, ComPtr<ID3D11DeviceContext> deviceContext, ShaderManager shaderManager) : IDisposable
    {
        private readonly ShaderManager _shaderManager = shaderManager ?? throw new ArgumentNullException(nameof(shaderManager));
        public List<Container3D> SceneObjects { get; } = [];
        public Lock SceneObjectLock { get; } = new();

        private readonly Queue<MapTile> tilesToLoad = new();
        private int totalTilesToLoad = 0;
        private readonly Dictionary<uint, uint> uuidUsers = [];
        private readonly HashSet<MapTile> loadedTiles = [];

        private WDT? currentWDT;
        public uint CurrentWDTFileDataID { get; private set; } = 775971;

        private uint OpsPerFrame = 5;
        private uint CurrentOps = 0;

        public Container3D? SelectedObject { get; set; } = null;

        private DebugRenderer? debugRenderer;
        public bool ShowBoundingBoxes { get; set; } = false;
        public bool ShowBoundingSpheres { get; set; } = false;

        public bool RenderADT { get; set; } = true;
        public bool RenderWMO { get; set; } = true;
        public bool RenderM2 { get; set; } = true;

        public Vector3 LightDirection { get; set; } = new Vector3(0.5f, 1f, 0.5f);

        private ComPtr<ID3D11ShaderResourceView> defaultTexture;

        // Instance rendering
        //private uint instanceMatrixVBO;
        //private const int MaxInstancesPerBatch = 1024;

        private CompiledShader adtShaderProgram;
        private CompiledShader wmoShaderProgram;
        private CompiledShader m2ShaderProgram;
        private CompiledShader debugShaderProgram;

        public readonly Dictionary<(uint FileDataID, int EnabledGroupHash), List<WMOContainer>> wmoInstances = [];
        public readonly Dictionary<uint, List<M2Container>> m2Instances = [];

        private static RenderState lastRenderState;
        private struct RenderState
        {
            public byte lastWMOVertexShaderID;
            public byte lastWMOPixelShaderID;
        }

        private int m2AlphaRefLoc;
        private int wmoAlphaRefLoc;

        private readonly ComPtr<ID3D11Device> _device = device;
        private readonly ComPtr<IDXGISwapChain1> _swapChain = swapchain;
        private ComPtr<ID3D11Buffer> perObjectConstantBuffer = default;
        private ComPtr<ID3D11Buffer> layerDataConstantBuffer = default;
        private ComPtr<ID3D11Buffer> wmoPerObjectConstantBuffer = default;
        private ComPtr<ID3D11Buffer> m2PerObjectConstantBuffer = default;
        private ComPtr<ID3D11Buffer> instanceMatrixBuffer = default;
        private ComPtr<ID3D11DepthStencilView> depthStencilView = default;
        private ComPtr<ID3D11Texture2D> depthTexture = default;
        private ComPtr<ID3D11SamplerState> textureSampler = default;
        private ComPtr<ID3D11SamplerState> clampSampler = default;
        private ComPtr<ID3D11RenderTargetView> renderTargetView = default;
        private ComPtr<ID3D11RasterizerState> rasterizerState = default;
        private ComPtr<ID3D11RasterizerState> wmoRasterizerState = default;
        private ComPtr<ID3D11ClassInstance> nullClassInstance = default;

        private uint _renderWidth = 1920;
        private uint _renderHeight = 1080;

        public int visibleChunks { get; private set; } = 0;
        public int visibleWMOs { get; private set; } = 0;
        public int visibleM2s { get; private set; } = 0;

        public bool SceneLoaded => loadedTiles.Count > 0; // this won't work for WMO only maps
        public string StatusMessage { get; private set; } = "";

        public void Initialize(ShaderManager shaderManager, CompiledShader adtShader, CompiledShader wmoShader, CompiledShader m2Shader, CompiledShader debugShader)
        {
            adtShaderProgram = adtShader;
            wmoShaderProgram = wmoShader;
            m2ShaderProgram = m2Shader;
            debugShaderProgram = debugShader;

            // debugRenderer = new DebugRenderer(_gl, debugShaderProgram);
            defaultTexture = BLPLoader.CreatePlaceholderTexture(device);
            //SetupInstanceBuffer();

            // Create PerObject constant buffer (matches cbuffer PerObject in adt.hlsl)
            unsafe
            {
                var perObjectSize = (uint)Marshal.SizeOf<ADTPerObjectCB>();
                var bufferDesc = new BufferDesc
                {
                    ByteWidth = perObjectSize,
                    Usage = Usage.Default,
                    BindFlags = (uint)BindFlag.ConstantBuffer
                };

                // Create buffer without initial data. We'll update it later via UpdateSubresource.
                SilkMarshal.ThrowHResult(_device.CreateBuffer(in bufferDesc, null, ref perObjectConstantBuffer));

                var layerDataSize = (uint)Marshal.SizeOf<LayerData>();
                bufferDesc = new BufferDesc
                {
                    ByteWidth = layerDataSize,
                    Usage = Usage.Default,
                    BindFlags = (uint)BindFlag.ConstantBuffer
                };

                SilkMarshal.ThrowHResult(device.CreateBuffer(in bufferDesc, null, ref layerDataConstantBuffer));

                bufferDesc = new BufferDesc
                {
                    ByteWidth = (uint)sizeof(WMOPerObjectCB),
                    Usage = Usage.Default,
                    BindFlags = (uint)BindFlag.ConstantBuffer
                };

                SilkMarshal.ThrowHResult(device.CreateBuffer(in bufferDesc, null, ref wmoPerObjectConstantBuffer));

                bufferDesc = new BufferDesc
                {
                    ByteWidth = (uint)sizeof(M2PerObjectCB),
                    Usage = Usage.Default,
                    BindFlags = (uint)BindFlag.ConstantBuffer
                };

                SilkMarshal.ThrowHResult(device.CreateBuffer(in bufferDesc, null, ref m2PerObjectConstantBuffer));

                //bufferDesc = new BufferDesc
                //{
                //    ByteWidth = (uint)(MaxInstancesPerBatch * sizeof(Matrix4x4)),
                //    Usage = Usage.Default,
                //    BindFlags = (uint)BindFlag.VertexBuffer
                //};

                //SilkMarshal.ThrowHResult(device.CreateBuffer(in bufferDesc, null, ref instanceMatrixBuffer));

                var rastDesc = new RasterizerDesc
                {
                    FillMode = FillMode.Solid,
                    CullMode = CullMode.Back, // TODO: Fix, then merge rasterizers
                    FrontCounterClockwise = false,
                    DepthClipEnable = true
                };

                SilkMarshal.ThrowHResult(device.CreateRasterizerState(in rastDesc, ref rasterizerState));
                deviceContext.RSSetState(rasterizerState);

                var wmoRastDesc = new RasterizerDesc
                {
                    FillMode = FillMode.Solid,
                    CullMode = CullMode.Front,
                    FrontCounterClockwise = false,
                    DepthClipEnable = true
                };
                SilkMarshal.ThrowHResult(device.CreateRasterizerState(in wmoRastDesc, ref wmoRasterizerState));

                ComPtr<ID3D11RasterizerState> rastState = default;
                device.CreateRasterizerState(in rastDesc, ref rastState);
                deviceContext.RSSetState(rastState);

                // Create sampler states (moved from render loop)
                var samplerDesc = new SamplerDesc
                {
                    Filter = Filter.MinMagMipLinear,
                    AddressU = TextureAddressMode.Wrap,
                    AddressV = TextureAddressMode.Wrap,
                    AddressW = TextureAddressMode.Wrap,
                    MipLODBias = 0,
                    MaxAnisotropy = 1,
                    MinLOD = float.MinValue,
                    MaxLOD = float.MaxValue,
                };
                samplerDesc.BorderColor[0] = 0.0f;
                samplerDesc.BorderColor[1] = 0.0f;
                samplerDesc.BorderColor[2] = 0.0f;
                samplerDesc.BorderColor[3] = 1.0f;

                SilkMarshal.ThrowHResult(device.CreateSamplerState(in samplerDesc, ref textureSampler));

                var clampSamplerDesc = new SamplerDesc
                {
                    Filter = Filter.MinMagMipLinear,
                    AddressU = TextureAddressMode.Clamp,
                    AddressV = TextureAddressMode.Clamp,
                    AddressW = TextureAddressMode.Clamp,
                    MipLODBias = 0,
                    MaxAnisotropy = 1,
                    MinLOD = float.MinValue,
                    MaxLOD = float.MaxValue,
                };
                clampSamplerDesc.BorderColor[0] = 0.0f;
                clampSamplerDesc.BorderColor[1] = 0.0f;
                clampSamplerDesc.BorderColor[2] = 0.0f;
                clampSamplerDesc.BorderColor[3] = 1.0f;

                SilkMarshal.ThrowHResult(device.CreateSamplerState(in clampSamplerDesc, ref clampSampler));

                CreateSizeDependentResources(1920, 1080);
            }
        }

        private unsafe void CreateSizeDependentResources(uint width, uint height)
        {
            using var framebuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            SilkMarshal.ThrowHResult(_device.CreateRenderTargetView(framebuffer, null, ref renderTargetView));

            Texture2DDesc backbufferDesc = default;
            framebuffer.GetDesc(ref backbufferDesc);
            uint actualWidth = backbufferDesc.Width;
            uint actualHeight = backbufferDesc.Height;

            var depthDesc = new Texture2DDesc
            {
                Width = actualWidth,
                Height = actualHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.FormatD24UnormS8Uint,
                SampleDesc = new SampleDesc { Count = 1, Quality = 0 },
                Usage = Usage.Default,
                BindFlags = (uint)BindFlag.DepthStencil,
            };
            SilkMarshal.ThrowHResult(_device.CreateTexture2D(in depthDesc, null, ref depthTexture));
            SilkMarshal.ThrowHResult(_device.CreateDepthStencilView(depthTexture, null, ref depthStencilView));

            var viewport = new Viewport
            {
                TopLeftX = 0,
                TopLeftY = 0,
                Width = actualWidth,
                Height = actualHeight,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            };
            deviceContext.RSSetViewports(1, in viewport);

            _renderWidth = actualWidth;
            _renderHeight = actualHeight;
        }


        public unsafe void Resize(uint width, uint height)
        {
            deviceContext.OMSetRenderTargets(0, (ID3D11RenderTargetView**)null, (ID3D11DepthStencilView*)null);

            if (renderTargetView.Handle != null) { renderTargetView.Dispose(); renderTargetView = default; }
            if (depthStencilView.Handle != null) { depthStencilView.Dispose(); depthStencilView = default; }
            if (depthTexture.Handle != null) { depthTexture.Dispose(); depthTexture = default; }

            CreateSizeDependentResources(width, height);
        }

        public void LoadWDT(uint wdtFileDataID)
        {
            if (CurrentWDTFileDataID != wdtFileDataID)
            {
                loadedTiles.Clear();

                lock (SceneObjectLock)
                    SceneObjects.Clear();

                CurrentWDTFileDataID = wdtFileDataID;
                currentWDT = WDTCache.GetOrLoad(CurrentWDTFileDataID);
            }
        }

        public void PreloadTEX()
        {
            if (currentWDT == null)
                return;

            var texFileDataID = currentWDT.Value.mphd.texFDID;
            if (texFileDataID != 0)
                TEXCache.Preload(texFileDataID);
        }

        public WDT? GetCurrentWDT()
        {
            currentWDT ??= WDTCache.GetOrLoad(CurrentWDTFileDataID);
            return currentWDT;
        }

        public (byte x, byte y) GetFirstMapTile()
        {
            if (currentWDT == null || currentWDT.Value.tiles.Count == 0)
                return (0, 0);

            return currentWDT.Value.tiles[0];
        }

        public void UpdateTilesByCameraPos(Vector3 cameraPosition)
        {
            if (currentWDT == null)
                return;

            var (x, y) = GetTileFromPosition(cameraPosition);

            var usedTiles = new List<MapTile>();

            var viewDistance = 2;
            for (int xOffset = -viewDistance; xOffset <= viewDistance; xOffset++)
            {
                for (int yOffset = -viewDistance; yOffset <= viewDistance; yOffset++)
                {
                    byte tileX = (byte)(x + xOffset);
                    byte tileY = (byte)(y + yOffset);

                    if (tileX < 0 || tileX > 63 || tileY < 0 || tileY > 63)
                        continue;

                    if (!currentWDT.Value.tiles.Contains((tileX, tileY)))
                        continue;

                    var mapTile = new MapTile
                    {
                        tileX = tileX,
                        tileY = tileY,
                        wdtFileDataID = CurrentWDTFileDataID
                    };

                    usedTiles.Add(mapTile);

                    if (!loadedTiles.Contains(mapTile) && !tilesToLoad.Contains(mapTile))
                    {
                        tilesToLoad.Enqueue(mapTile);
                        totalTilesToLoad++;
                    }
                }
            }

            foreach (var tile in loadedTiles.ToList())
            {
                if (!usedTiles.Contains(tile))
                {
                    loadedTiles.Remove(tile);

                    lock (SceneObjectLock)
                    {
                        UpdateInstanceList();

                        var adtToRemove = SceneObjects.FirstOrDefault(x => x is ADTContainer adt && adt.mapTile.wdtFileDataID == tile.wdtFileDataID && adt.mapTile.tileX == tile.tileX && adt.mapTile.tileY == tile.tileY) as ADTContainer;
                        if (adtToRemove != null)
                        {
                            SceneObjects.Remove(adtToRemove);
                            ADTCache.Release(device, adtToRemove.mapTile, adtToRemove.mapTile.wdtFileDataID);

                            List<WMOContainer> wmosToRemove = [.. SceneObjects.Where(x => x is WMOContainer wmo && wmo.ParentFileDataId == adtToRemove.Terrain.rootADTFileDataID).Select(x => (WMOContainer)x)];
                            foreach (var wmo in wmosToRemove)
                            {
                                if (uuidUsers.TryGetValue(wmo.UniqueID, out var count))
                                {
                                    if (count > 1)
                                    {
                                        uuidUsers[wmo.UniqueID] = count - 1;
                                    }
                                    else
                                    {
                                        foreach (var doodad in wmo.ActiveDoodads)
                                        {
                                            SceneObjects.Remove(doodad);
                                            M2Cache.Release(doodad.FileDataId, doodad.ParentFileDataId);
                                        }
                                        wmo.ActiveDoodads.Clear();

                                        SceneObjects.Remove(wmo);
                                        WMOCache.Release(wmo.FileDataId, wmo.ParentFileDataId);
                                        uuidUsers.Remove(wmo.UniqueID);
                                    }
                                }
                            }

                            List<M2Container> m2sToRemove = [.. SceneObjects.Where(x => x is M2Container m2 && m2.ParentFileDataId == adtToRemove.Terrain.rootADTFileDataID).Select(x => (M2Container)x)];
                            foreach (var m2 in m2sToRemove)
                            {
                                SceneObjects.Remove(m2);
                                M2Cache.Release(m2.FileDataId, m2.ParentFileDataId);
                            }
                        }
                    }
                }
            }

            if (loadedTiles.Count == 0)
            {
                if (WMOCache.GetCacheCount() > 0)
                    WMOCache.ReleaseAll();

                if (M2Cache.GetCacheCount() > 0)
                    M2Cache.ReleaseAll();

                if (BLPCache.GetCacheCount() > 0)
                    BLPCache.ReleaseAll();
            }
        }

        public void UpdateM2InstanceList()
        {
            m2Instances.Clear();
            foreach (var sceneObject in SceneObjects)
            {
                if (sceneObject is M2Container m2)
                {
                    if (!m2Instances.ContainsKey(m2.FileDataId))
                        m2Instances[m2.FileDataId] = [];
                    m2Instances[m2.FileDataId].Add(m2);
                }
            }
        }

        public void UpdateWMOInstanceList()
        {
            wmoInstances.Clear();
            foreach (var sceneObject in SceneObjects)
            {
                if (sceneObject is WMOContainer wmo)
                {
                    var key = (wmo.FileDataId, wmo.EnabledGroups.GetHashCode());
                    if (!wmoInstances.ContainsKey(key))
                        wmoInstances[key] = [];
                    wmoInstances[key].Add(wmo);
                }
            }
        }

        public void UpdateInstanceList()
        {
            wmoInstances.Clear();
            m2Instances.Clear();

            foreach (var sceneObject in SceneObjects)
            {
                if (sceneObject is WMOContainer wmo)
                {
                    var key = (wmo.FileDataId, wmo.EnabledGroups.GetHashCode());
                    if (!wmoInstances.ContainsKey(key))
                        wmoInstances[key] = [];

                    wmoInstances[key].Add(wmo);
                }
                else if (sceneObject is M2Container m2)
                {
                    if (!m2Instances.ContainsKey(m2.FileDataId))
                        m2Instances[m2.FileDataId] = [];

                    m2Instances[m2.FileDataId].Add(m2);
                }
            }
        }

        private void SpawnWMODoodads(WMOContainer wmoContainer)
        {
            var wmo = WMOCache.GetOrLoad(device, wmoContainer.FileDataId, wmoShaderProgram, wmoContainer.ParentFileDataId, false);
            var enabledSets = wmoContainer.EnabledDoodadSets;

            wmoContainer.ActiveDoodads.Clear();

            foreach (var doodad in wmo.doodads)
            {
                if (!enabledSets[doodad.doodadSet])
                    continue;

                var m2Container = new M2Container(device, doodad.filedataid, m2ShaderProgram, wmoContainer.ParentFileDataId)
                {
                    ParentWMO = wmoContainer,
                    LocalPosition = doodad.position,
                    LocalRotation = doodad.rotation,
                    LocalScale = doodad.scale,
                };

                lock (SceneObjectLock)
                    SceneObjects.Add(m2Container);

                wmoContainer.ActiveDoodads.Add(m2Container);
            }
        }

        public void RefreshWMODoodads(WMOContainer wmoContainer)
        {
            if (!wmoContainer.IsLoaded)
                return;

            lock (SceneObjectLock)
            {
                foreach (var doodad in wmoContainer.ActiveDoodads)
                {
                    SceneObjects.Remove(doodad);
                    M2Cache.Release(doodad.FileDataId, doodad.ParentFileDataId);
                }

                SpawnWMODoodads(wmoContainer);

                UpdateInstanceList();
            }
        }

        public bool ProcessQueue()
        {
            // If no ADTs are queued, but other files still are, we return true (and not dequeue tiles) to keep calling this function over and over to handle the various uploads, because these need to be called from this thread, but this does block new ADTs from loading until these are done which isn't ideal.

            // WMO
            WMOCache.Upload(wmoShaderProgram);

            // BLP
            BLPCache.Upload();

            if (tilesToLoad.Count == 0)
            {
                var wmoRemaining = WMOCache.GetLoadQueueCount();
                var blpRemaining = BLPCache.GetQueueCount();

                if (wmoRemaining > 0)
                {
                    StatusMessage = $"Loading WMOs ({wmoRemaining} queued)...";
                    return true;
                }
                else if (blpRemaining > 0)
                {
                    StatusMessage = $"Loading textures ({blpRemaining} queued)...";
                    return true;
                }
                else
                {
                    // Nothing to do, clear status and return
                    StatusMessage = "";
                    return false;
                }
            }

            // TODO: M2

            var mapTile = tilesToLoad.Dequeue();
            var tilesLoaded = totalTilesToLoad - tilesToLoad.Count;
            var wmoQueueCount = WMOCache.GetLoadQueueCount();
            var blpQueueCount = BLPCache.GetQueueCount();
            StatusMessage = $"Loading tile {mapTile.tileX},{mapTile.tileY} ({tilesLoaded}/{totalTilesToLoad})";

            if (wmoQueueCount > 0)
                StatusMessage += $" | (busy loading WMOs ({wmoQueueCount} queued)";

            if (blpQueueCount > 0)
                StatusMessage += $" | (busy loading textures ({blpQueueCount} queued)";

            var timer = new Stopwatch();
            timer.Start();

            Terrain adt;

            try
            {
                adt = ADTCache.GetOrLoad(device, mapTile, adtShaderProgram, mapTile.wdtFileDataID);
                CurrentOps++;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading ADT: " + ex.ToString());
                return false;
            }

            timer.Stop();

            var adtContainer = new ADTContainer(device, adt, mapTile, adtShaderProgram);

            lock (SceneObjectLock)
                SceneObjects.Add(adtContainer);

            foreach (var worldModel in adt.worldModelBatches)
            {
                if (uuidUsers.ContainsKey(worldModel.uniqueID))
                    continue;

                WMOCache.GetOrLoad(device, worldModel.fileDataID, wmoShaderProgram, adt.rootADTFileDataID);

                var worldModelContainer = new WMOContainer(device, worldModel.fileDataID, wmoShaderProgram, adt.rootADTFileDataID)
                {
                    Position = worldModel.position,
                    Rotation = worldModel.rotation,
                    Scale = worldModel.scale == 0 ? 1 : worldModel.scale,
                    UniqueID = worldModel.uniqueID,
                    OnDoodadSetsChanged = RefreshWMODoodads
                };

                worldModelContainer.DoodadSetsToEnable.AddRange(worldModel.doodadSetIDs);

                lock (SceneObjectLock)
                    SceneObjects.Add(worldModelContainer);

                if (uuidUsers.TryGetValue(worldModel.uniqueID, out var count))
                    uuidUsers[worldModel.uniqueID] = count + 1;
                else
                    uuidUsers[worldModel.uniqueID] = 1;
            }

            var wmosToSpawn = SceneObjects.OfType<WMOContainer>().Where(w => w.IsLoaded && !w.DoodadsSpawned).ToList();
            foreach (var wmoContainer in wmosToSpawn)
            {
                SpawnWMODoodads(wmoContainer);
                wmoContainer.DoodadsSpawned = true;
            }

            foreach (var doodad in adt.doodads)
            {
                var doodadContainer = new M2Container(device, doodad.fileDataID, m2ShaderProgram, adt.rootADTFileDataID)
                {
                    Position = doodad.position,
                    Rotation = doodad.rotation,
                    Scale = doodad.scale
                };

                lock (SceneObjectLock)
                    SceneObjects.Add(doodadContainer);
            }

            UpdateInstanceList();

            loadedTiles.Add(mapTile);

            return true;
        }

        public void PerformRaycast(float mouseX, float mouseY, Camera camera, int windowWidth, int windowHeight)
        {
            // TODO: Untested with DX, bounding boxes likely need accurate transforming 
            var ray = camera.GetRayFromScreen(mouseX, mouseY, windowWidth, windowHeight);

            Container3D? closestObject = null;
            float closestDistance = float.MaxValue;

            lock (SceneObjectLock)
            {
                foreach (var sceneObject in SceneObjects)
                {
                    if (sceneObject is ADTContainer)
                        continue;

                    if (!RenderWMO && sceneObject is WMOContainer)
                        continue;

                    if (!RenderM2 && sceneObject is M2Container)
                        continue;

                    // Make doodads unselectable
                    if (sceneObject is M2Container m2container && m2container.ParentWMO != null)
                        continue;

                    if (sceneObject.IsSelected)
                        continue;

                    var sphere = sceneObject.GetBoundingSphere();
                    if (sphere.HasValue)
                    {
                        if (IntersectionTests.RayIntersectsSphere(ray, sphere.Value, out float sphereDistance))
                        {
                            if (sphereDistance < closestDistance)
                            {
                                var box = sceneObject.GetBoundingBox();
                                if (box.HasValue && IntersectionTests.RayIntersectsBox(ray, box.Value, out float boxDistance))
                                {
                                    if (boxDistance < closestDistance)
                                    {
                                        closestDistance = boxDistance;
                                        closestObject = sceneObject;
                                    }
                                }
                                else if (!box.HasValue)
                                {
                                    closestDistance = sphereDistance;
                                    closestObject = sceneObject;
                                }
                            }
                        }
                    }
                }
            }

            SelectedObject?.IsSelected = false;
            SelectedObject = closestObject;
            SelectedObject?.IsSelected = true;
        }

        public void RenderScene(Camera camera, out bool gizmoWasUsing, out bool gizmoWasOver)
        {
            deviceContext.RSSetState(rasterizerState);

#if DEBUG
            if (shaderManager.CheckForChanges())
            {
                adtShaderProgram = shaderManager.GetOrCompileShader("adt");
                wmoShaderProgram = shaderManager.GetOrCompileShader("wmo");
                m2ShaderProgram = shaderManager.GetOrCompileShader("m2");
            }
#endif

            var projectionMatrix = camera.GetProjectionMatrix();

            var cameraMatrix = camera.GetViewMatrix();

            camera.UpdateFrustum();

            visibleM2s = 0;
            visibleWMOs = 0;
            visibleChunks = 0;

            var backgroundColour = new[] { 1.0f, 0.0f, 0.0f, 1.0f };

            deviceContext.ClearRenderTargetView(renderTargetView, ref backgroundColour[0]);
            deviceContext.OMSetRenderTargets(1, ref renderTargetView, depthStencilView);
            deviceContext.ClearDepthStencilView(depthStencilView, (uint)ClearFlag.Depth, 1.0f, 0);

            deviceContext.PSSetSamplers(0, 1, ref textureSampler);
            deviceContext.PSSetSamplers(1, 1, ref clampSampler);

            deviceContext.IASetPrimitiveTopology(D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist);

            var adtVertexStride = (uint)Marshal.SizeOf<ADTVertex>();
            var adtVertexOffset = 0U;

            uint wmoVertexStride = (uint)Marshal.SizeOf<WMOVertex>();
            uint wmoVertexOffset = 0;

            uint m2VertexStride = (uint)Marshal.SizeOf<M2Vertex>();
            uint m2VertexOffset = 0;

            foreach (var sceneObject in SceneObjects)
            {
                if (sceneObject is M2Container m2Container)
                {
                    if (!RenderM2)
                        break;

                    deviceContext.RSSetState(wmoRasterizerState); // can reuse this here for now, switch to generic one once adts are fixed

                    var m2 = M2Cache.GetOrLoad(device, m2Container.FileDataId, m2ShaderProgram, m2Container.ParentFileDataId);

                    deviceContext.IASetInputLayout(m2ShaderProgram.InputLayout);
                    deviceContext.VSSetShader(m2ShaderProgram.VertexShader, ref nullClassInstance, 0);
                    deviceContext.PSSetShader(m2ShaderProgram.PixelShader, ref nullClassInstance, 0);
                    deviceContext.PSSetSamplers(0, 1, ref textureSampler);

                    var vertexBuffer = m2.vertexBuffer;
                    var indiceBuffer = m2.indiceBuffer;

                    deviceContext.IASetVertexBuffers(0, 1, ref vertexBuffer, in m2VertexStride, in m2VertexOffset);
                    deviceContext.IASetIndexBuffer(indiceBuffer, Format.FormatR16Uint, 0);

                    var cb = new M2PerObjectCB
                    {
                        projection_matrix = projectionMatrix,
                        view_matrix = cameraMatrix,
                        model_matrix = m2Container.GetModelMatrix(),
                        vertexShader = 0,
                        pixelShader = 0,
                        texMatrix1 = Matrix4x4.Identity, // todo
                        texMatrix2 = Matrix4x4.Identity, // todo
                        hasTexMatrix1 = 0, // todo
                        hasTexMatrix2 = 0, // todo
                        lightDirection = LightDirection,
                        alphaRef = 1.0f, // todo
                        blendMode = 0,
                        _pad = Vector3.Zero
                    };

                    deviceContext.VSSetConstantBuffers(0, 1, ref m2PerObjectConstantBuffer);
                    deviceContext.PSSetConstantBuffers(0, 1, ref m2PerObjectConstantBuffer);

                    for (var j = 0; j < m2.submeshes.Length; j++)
                    {
                        var batch = m2.submeshes[j];

                        cb.vertexShader = (int)batch.vertexShaderID;
                        cb.pixelShader = (int)batch.pixelShaderID;
                        cb.blendMode = batch.blendType;

                        deviceContext.UpdateSubresource(m2PerObjectConstantBuffer, 0, ref Unsafe.NullRef<Box>(), ref cb, 0, 0);

                        var srvs = batch.material.Select(id => id != 0 ? BLPCache.GetCurrent(id, defaultTexture) : defaultTexture).ToArray();
                        if (srvs.Length > 0)
                            deviceContext.PSSetShaderResources(0, (uint)srvs.Length, ref srvs[0]);

                        deviceContext.DrawIndexed(batch.numFaces, batch.firstFace, 0);
                    }
                }
                else if (sceneObject is WMOContainer wmoContainer)
                {
                    if (!RenderWMO)
                        break;

                    if (!wmoContainer.IsLoaded)
                        continue;

                    deviceContext.RSSetState(wmoRasterizerState);

                    var wmo = WMOCache.GetOrLoad(device, wmoContainer.FileDataId, wmoShaderProgram, wmoContainer.ParentFileDataId);
                    var enabledGroups = wmoContainer.EnabledGroups;

                    deviceContext.IASetInputLayout(wmoShaderProgram.InputLayout);
                    deviceContext.VSSetShader(wmoShaderProgram.VertexShader, ref nullClassInstance, 0);
                    deviceContext.PSSetShader(wmoShaderProgram.PixelShader, ref nullClassInstance, 0);
                    deviceContext.PSSetSamplers(0, 1, ref textureSampler);

                    var cb = new WMOPerObjectCB
                    {
                        projection_matrix = projectionMatrix,
                        view_matrix = cameraMatrix,
                        model_matrix = wmoContainer.GetModelMatrix(),
                        vertexShader = 0,
                        pixelShader = 0,
                        _pad0 = Vector2.Zero,
                        lightDirection = LightDirection,
                        alphaRef = 1.0f, // todo
                    };

                    deviceContext.VSSetConstantBuffers(0, 1, ref wmoPerObjectConstantBuffer);
                    deviceContext.PSSetConstantBuffers(0, 1, ref wmoPerObjectConstantBuffer);

                    for (var j = 0; j < wmo.wmoRenderBatch.Length; j++)
                    {
                        var batch = wmo.wmoRenderBatch[j];

                        if (enabledGroups[batch.groupID] == false)
                            continue;

                        var group = wmo.groupBatches[batch.groupID];
                        var vertexBuffer = group.vertexBuffer;
                        var indiceBuffer = group.indiceBuffer;

                        deviceContext.IASetVertexBuffers(0, 1, ref vertexBuffer, in wmoVertexStride, in wmoVertexOffset);
                        deviceContext.IASetIndexBuffer(indiceBuffer, Format.FormatR16Uint, 0);

                        cb.vertexShader = (int)ShaderEnums.WMOShaders[(int)batch.shader].VertexShader;
                        cb.pixelShader = (int)ShaderEnums.WMOShaders[(int)batch.shader].PixelShader;

                        deviceContext.UpdateSubresource(wmoPerObjectConstantBuffer, 0, ref Unsafe.NullRef<Box>(), ref cb, 0, 0);

                        var srvs = batch.materialFDIDs.Select(id => id != 0 ? BLPCache.GetCurrent(id, defaultTexture) : defaultTexture).ToArray();
                        if (srvs.Length > 0)
                            deviceContext.PSSetShaderResources(0, (uint)srvs.Length, ref srvs[0]);

                        deviceContext.DrawIndexed(batch.numFaces, batch.firstFace, 0);
                    }

                }
                else if (sceneObject is ADTContainer adt)
                {
                    if (!RenderADT)
                        continue;

                    deviceContext.RSSetState(rasterizerState);

                    var vertexBuffer = adt.Terrain.vertexBuffer;
                    var indiceBuffer = adt.Terrain.indiceBuffer;

                    var cb = new ADTPerObjectCB
                    {
                        model_matrix = adt.GetModelMatrix(),
                        projection_matrix = projectionMatrix,
                        rotation_matrix = cameraMatrix,
                        firstPos = adt.Terrain.startPos,
                        _pad0 = 0f
                    };

                    deviceContext.UpdateSubresource(perObjectConstantBuffer, 0, ref Unsafe.NullRef<Box>(), ref cb, 0, 0);
                    deviceContext.VSSetConstantBuffers(0, 1, ref perObjectConstantBuffer);
                    deviceContext.PSSetConstantBuffers(0, 1, ref perObjectConstantBuffer);

                    deviceContext.IASetInputLayout(adtShaderProgram.InputLayout);
                    deviceContext.IASetVertexBuffers(0, 1, ref vertexBuffer, in adtVertexStride, in adtVertexOffset);
                    deviceContext.IASetIndexBuffer(indiceBuffer, Format.FormatR32Uint, 0);

                    deviceContext.VSSetShader(adtShaderProgram.VertexShader, ref nullClassInstance, 0);
                    deviceContext.PSSetShader(adtShaderProgram.PixelShader, ref nullClassInstance, 0);

                    deviceContext.VSSetConstantBuffers(1, 1, ref layerDataConstantBuffer);
                    deviceContext.PSSetConstantBuffers(1, 1, ref layerDataConstantBuffer);

                    var layerCB = new LayerData
                    {
                        layerCount = 0,
                        lightDirection = LightDirection,
                        heightScales0 = Vector4.One,
                        heightScales1 = Vector4.One,
                        heightOffsets0 = Vector4.Zero,
                        heightOffsets1 = Vector4.Zero,
                        layerScales0 = Vector4.One,
                        layerScales1 = Vector4.One,
                    };

                    for (uint c = 0; c < 256; c++)
                    {
                        var batch = adt.Terrain.renderBatches[c];

                        layerCB.layerCount = batch.materialID.Length;
                        layerCB.heightScales0 = new Vector4(batch.heightScales[0], batch.heightScales[1], batch.heightScales[2], batch.heightScales[3]);
                        layerCB.heightScales1 = new Vector4(batch.heightScales[4], batch.heightScales[5], batch.heightScales[6], batch.heightScales[7]);
                        layerCB.heightOffsets0 = new Vector4(batch.heightOffsets[0], batch.heightOffsets[1], batch.heightOffsets[2], batch.heightOffsets[3]);
                        layerCB.heightOffsets1 = new Vector4(batch.heightOffsets[4], batch.heightOffsets[5], batch.heightOffsets[6], batch.heightOffsets[7]);
                        layerCB.layerScales0 = new Vector4(batch.scales[0], batch.scales[1], batch.scales[2], batch.scales[3]);
                        layerCB.layerScales1 = new Vector4(batch.scales[4], batch.scales[5], batch.scales[6], batch.scales[7]);

                        deviceContext.UpdateSubresource(layerDataConstantBuffer, 0, ref Unsafe.NullRef<Box>(), ref layerCB, 0, 0);

                        deviceContext.PSSetShaderResources(0, 8, ref batch.materialID[0]);
                        deviceContext.PSSetShaderResources(8, 8, ref batch.heightMaterialIDs[0]);
                        deviceContext.PSSetShaderResources(16, 2, ref batch.alphaMaterialID[0]);

                        deviceContext.DrawIndexed(768, c * 768, 0);
                    }
                }
            }

            swapchain.Present(1, 0);

            gizmoWasUsing = false;
            gizmoWasOver = false;
        }

        public void RenderDebug(Camera camera, out bool gizmoWasUsing, out bool gizmoWasOver)
        {
            if (debugRenderer == null)
            {
                gizmoWasUsing = false;
                gizmoWasOver = false;
                return;
            }

            debugRenderer.Clear();

            gizmoWasUsing = false;
            gizmoWasOver = false;

            lock (SceneObjectLock)
            {
                foreach (var sceneObject in SceneObjects)
                {
                    if (sceneObject is ADTContainer)
                        continue;

                    if (!RenderWMO && sceneObject is WMOContainer && !sceneObject.IsSelected)
                        continue;

                    if (!RenderM2 && sceneObject is M2Container && !sceneObject.IsSelected)
                        continue;

                    var color = sceneObject.IsSelected ? new Vector4(0, 1, 0, 1) : new Vector4(1, 1, 0, 1);

                    if (ShowBoundingBoxes || sceneObject.IsSelected)
                    {
                        var box = sceneObject.GetBoundingBox();
                        if (box.HasValue)
                        {
                            debugRenderer.DrawBox(box.Value.Min, box.Value.Max, color);
                        }
                    }

                    if (ShowBoundingSpheres)
                    {
                        var sphere = sceneObject.GetBoundingSphere();
                        if (sphere.HasValue)
                        {
                            debugRenderer.DrawSphere(sphere.Value.Center, sphere.Value.Radius, color);
                        }
                    }
                }
            }

            var debugViewMatrix = camera.GetViewMatrix();
            debugViewMatrix *= Matrix4x4.CreateRotationZ(MathF.PI / 180f * 180f);
            var projectionMatrix = camera.GetProjectionMatrix();
            debugRenderer.Render(projectionMatrix, debugViewMatrix);
        }

        public static (byte x, byte y) GetTileFromPosition(Vector3 position)
        {
            const float tileSize = 533.33333f;
            const int mapCenter = 32;

            var posX = position.Y / tileSize;
            var posY = position.X / tileSize;

            int tileX = mapCenter - (int)Math.Ceiling(posX);
            int tileY = mapCenter - (int)Math.Ceiling(posY);

            tileX = Math.Clamp(tileX, 0, 63);
            tileY = Math.Clamp(tileY, 0, 63);

            return ((byte)tileX, (byte)tileY);
        }

        public static Vector3 GetTileCenterPosition(byte tileX, byte tileY)
        {
            const float tileSize = 533.33333f;
            const int mapCenter = 32;
            var posX = (mapCenter - tileX) * tileSize - (tileSize / 2);
            var posY = (mapCenter - tileY) * tileSize - (tileSize / 2);
            return new Vector3(posY, posX, 0);
        }

        //private unsafe void SetupInstanceBuffer()
        //{
        //    instanceMatrixVBO = _gl.GenBuffer();
        //    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, instanceMatrixVBO);
        //    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(MaxInstancesPerBatch * sizeof(float) * 16), null, BufferUsageARB.DynamicDraw);
        //    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        //}

        //private unsafe void SetupInstanceAttributes(uint vao)
        //{
        //    _gl.BindVertexArray(vao);
        //    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, instanceMatrixVBO);

        //    for (uint i = 0; i < 4; i++)
        //    {
        //        uint location = 10 + i;
        //        _gl.EnableVertexAttribArray(location);
        //        _gl.VertexAttribPointer(location, 4, VertexAttribPointerType.Float, false, sizeof(float) * 16, (void*)(sizeof(float) * 4 * i));
        //        _gl.VertexAttribDivisor(location, 1);
        //    }

        //    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        //    _gl.BindVertexArray(0);
        //}

        //private unsafe uint MakeDefaultTexture()
        //{
        //    var defaultTexture = _gl.GenTexture();
        //    _gl.BindTexture(TextureTarget.Texture2D, defaultTexture);
        //    byte[] fill = [0, 0, 0, 0];
        //    fixed (byte* fillPtr = fill)
        //    {
        //        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, fillPtr);
        //    }

        //    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        //    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        //    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        //    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

        //    return defaultTexture;
        //}

        //private static void SwitchBlendMode(int blendType, GL gl, int alphaRefLoc)
        //{
        //    switch (blendType)
        //    {
        //        case 0:
        //            gl.Disable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            break;
        //        case 1:
        //            gl.Disable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, 0.90393700787f);
        //            break;
        //        case 2:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        //            break;
        //        case 3:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);
        //            break;
        //        case 4:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFuncSeparate(BlendingFactor.DstColor, BlendingFactor.Zero, BlendingFactor.DstAlpha, BlendingFactor.Zero);
        //            break;
        //        case 5:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFuncSeparate(BlendingFactor.DstColor, BlendingFactor.SrcColor, BlendingFactor.DstAlpha, BlendingFactor.SrcAlpha);
        //            break;
        //        case 6:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFuncSeparate(BlendingFactor.DstColor, BlendingFactor.One, BlendingFactor.DstAlpha, BlendingFactor.One);
        //            break;
        //        case 7:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFuncSeparate(BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One);
        //            break;
        //        case 8:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFuncSeparate(BlendingFactor.OneMinusSrcAlpha, BlendingFactor.Zero, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.Zero);
        //            break;
        //        case 9:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.Zero, BlendingFactor.SrcAlpha, BlendingFactor.Zero);
        //            break;
        //        case 10:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One);
        //            break;
        //        case 11:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFuncSeparate(BlendingFactor.ConstantAlpha, BlendingFactor.OneMinusConstantAlpha, BlendingFactor.ConstantAlpha, BlendingFactor.OneMinusConstantAlpha);
        //            break;
        //        case 12:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFuncSeparate(BlendingFactor.OneMinusDstColor, BlendingFactor.One, BlendingFactor.One, BlendingFactor.Zero);
        //            break;
        //        case 13:
        //            gl.Enable(EnableCap.Blend);
        //            gl.Uniform1(alphaRefLoc, -1.0f);
        //            gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        //            break;
        //        default:
        //            throw new Exception("Unsupport blend mode: " + blendType);
        //    }
        //}

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                //M2Cache.StopWorker();
                WMOCache.StopWorker();
                BLPCache.StopWorker();

                // TODO: Release all cached resources

                textureSampler.Dispose();
                clampSampler.Dispose();
                renderTargetView.Dispose();
                depthStencilView.Dispose();
                depthTexture.Dispose();
                perObjectConstantBuffer.Dispose();
                layerDataConstantBuffer.Dispose();
                m2PerObjectConstantBuffer.Dispose();
                wmoPerObjectConstantBuffer.Dispose();
                instanceMatrixBuffer.Dispose();
                defaultTexture.Dispose();
                debugRenderer?.Dispose();
            }
        }
    }
}
