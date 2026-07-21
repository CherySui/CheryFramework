using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Reflection;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.Win32;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D12.D3D12;
using static Vortice.DXGI.DXGI;
using HexaD3D12 = Hexa.NET.ImGui.Backends.D3D12;
using ImGuiImplD3D12 = Hexa.NET.ImGui.Backends.D3D12.ImGuiImplD3D12;
using ImGuiImplDX12InitInfo = Hexa.NET.ImGui.Backends.D3D12.ImGuiImplDX12InitInfo;
using ID3D12GraphicsCommandListPtr = Hexa.NET.ImGui.Backends.D3D12.ID3D12GraphicsCommandListPtr;

namespace CheryFramework.Preview;

public sealed unsafe class D3D12PreviewRenderer : IDisposable
{
    private const int BufferCount = 2;
    private const Format BackBufferFormat = Format.R8G8B8A8_UNorm;

    private readonly IDXGIFactory4 _factory;
    private readonly ID3D12Device _device;
    private readonly ID3D12CommandQueue _queue;
    private readonly IDXGISwapChain3 _swapChain;
    private readonly ID3D12DescriptorHeap _rtvHeap;
    private readonly ID3D12DescriptorHeap _srvHeap;
    private readonly ID3D12CommandAllocator _commandAllocator;
    private readonly ID3D12GraphicsCommandList _commandList;
    private readonly ID3D12Fence _fence;
    private readonly AutoResetEvent _fenceEvent = new(false);
    private readonly ID3D12Resource?[] _renderTargets = new ID3D12Resource?[BufferCount];
    private readonly uint _rtvDescriptorSize;
    private readonly uint _srvDescriptorSize;
    private readonly bool[] _srvDescriptorsUsed = new bool[64];
    private ID3D12Resource? _exampleBackgroundTexture;
    private int _exampleBackgroundDescriptorIndex = -1;
    private ImGuiContextPtr _imguiContext;
    private GCHandle _selfHandle;
    private ulong _fenceValue;
    private bool _imguiInitialized;
    private bool _disposed;
    private int _width;
    private int _height;

    public D3D12PreviewRenderer(nint windowHandle, int width, int height)
    {
        if (!D3D12.IsSupported(FeatureLevel.Level_11_0))
        {
            throw new PlatformNotSupportedException("当前系统或显卡不支持 Direct3D 12。");
        }

        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _factory = CreateDXGIFactory2<IDXGIFactory4>(false);
        _device = CreateDevice(_factory);
        _queue = _device.CreateCommandQueue(CommandListType.Direct);

        SwapChainDescription1 description = new()
        {
            Width = (uint)_width,
            Height = (uint)_height,
            Format = BackBufferFormat,
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = BufferCount,
            SwapEffect = SwapEffect.FlipDiscard,
            SampleDescription = new SampleDescription(1, 0)
        };

        using (IDXGISwapChain1 swapChain = _factory.CreateSwapChainForHwnd(_queue, windowHandle, description))
        {
            _swapChain = swapChain.QueryInterface<IDXGISwapChain3>();
        }
        _factory.MakeWindowAssociation(windowHandle, WindowAssociationFlags.IgnoreAltEnter);

        _rtvHeap = _device.CreateDescriptorHeap(new DescriptorHeapDescription(DescriptorHeapType.RenderTargetView, BufferCount));
        _srvHeap = _device.CreateDescriptorHeap(new DescriptorHeapDescription(
            DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
            64,
            DescriptorHeapFlags.ShaderVisible));
        _rtvDescriptorSize = _device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
        _srvDescriptorSize = _device.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);
        CreateRenderTargets();

        _commandAllocator = _device.CreateCommandAllocator(CommandListType.Direct);
        _commandList = _device.CreateCommandList<ID3D12GraphicsCommandList>(CommandListType.Direct, _commandAllocator);
        _commandList.Close();
        _fence = _device.CreateFence(0);

        InitializeImGui(windowHandle);
        LoadExampleBackground();
    }

    public void Render()
    {
        if (_disposed || _width <= 0 || _height <= 0) return;

        ImGui.SetCurrentContext(_imguiContext);
        ImGuiImplD3D12.NewFrame();
        ImGuiImplWin32.NewFrame();
        ImGui.NewFrame();
        PreviewPanel.Draw();
        ImGui.Render();

        uint backBufferIndex = _swapChain.CurrentBackBufferIndex;
        ID3D12Resource renderTarget = _renderTargets[backBufferIndex]!;
        CpuDescriptorHandle rtv = new(_rtvHeap.GetCPUDescriptorHandleForHeapStart(), (int)backBufferIndex, _rtvDescriptorSize);

        _commandAllocator.Reset();
        _commandList.Reset(_commandAllocator);
        _commandList.ResourceBarrierTransition(renderTarget, ResourceStates.Present, ResourceStates.RenderTarget);
        _commandList.OMSetRenderTargets(rtv, null);
        _commandList.ClearRenderTargetView(rtv, new Color4(0.024f, 0.028f, 0.036f, 1f));
        _commandList.SetDescriptorHeaps(_srvHeap);

        ImGuiImplD3D12.RenderDrawData(
            ImGui.GetDrawData(),
            new ID3D12GraphicsCommandListPtr((HexaD3D12.ID3D12GraphicsCommandList*)_commandList.NativePointer));

        _commandList.ResourceBarrierTransition(renderTarget, ResourceStates.RenderTarget, ResourceStates.Present);
        _commandList.Close();
        _queue.ExecuteCommandList(_commandList);
        _swapChain.Present(1, PresentFlags.None).CheckError();
        WaitForGpu();
    }

    public void Resize(int width, int height)
    {
        if (_disposed || width <= 0 || height <= 0 || (width == _width && height == _height)) return;

        WaitForGpu();
        ReleaseRenderTargets();
        _swapChain.ResizeBuffers(BufferCount, (uint)width, (uint)height, BackBufferFormat, SwapChainFlags.None).CheckError();
        _width = width;
        _height = height;
        CreateRenderTargets();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        WaitForGpu();

        if (_imguiInitialized)
        {
            ImGui.SetCurrentContext(_imguiContext);
            ImGuiImplD3D12.Shutdown();
            ImGuiImplWin32.Shutdown();
            ImGui.DestroyContext(_imguiContext);
            _imguiInitialized = false;
        }
        PreviewPanel.ClearExampleBackground();
        _exampleBackgroundTexture?.Dispose();
        _exampleBackgroundTexture = null;
        if (_exampleBackgroundDescriptorIndex >= 0)
        {
            _srvDescriptorsUsed[_exampleBackgroundDescriptorIndex] = false;
            _exampleBackgroundDescriptorIndex = -1;
        }
        if (_selfHandle.IsAllocated) _selfHandle.Free();

        ReleaseRenderTargets();
        _fence.Dispose();
        _fenceEvent.Dispose();
        _commandList.Dispose();
        _commandAllocator.Dispose();
        _srvHeap.Dispose();
        _rtvHeap.Dispose();
        _swapChain.Dispose();
        _queue.Dispose();
        _device.Dispose();
        _factory.Dispose();
    }

    private void InitializeImGui(nint windowHandle)
    {
        _imguiContext = ImGui.CreateContext();
        ImGui.SetCurrentContext(_imguiContext);
        ImGui.GetIO().Handle->IniFilename = null;

        string fontPath = ExtractFontToTemporaryFile();
        try
        {
            ImGui.AddFontFromFileTTF(ImGui.GetIO().Fonts, fontPath, 20f);
        }
        finally
        {
            if (File.Exists(fontPath)) File.Delete(fontPath);
        }
        ApplyStyle();

        ImGuiImplWin32.SetCurrentContext(_imguiContext);
        ImGuiImplD3D12.SetCurrentContext(_imguiContext);
        if (!ImGuiImplWin32.Init((void*)windowHandle))
        {
            throw new InvalidOperationException("ImGui Win32 backend 初始化失败。");
        }

        _selfHandle = GCHandle.Alloc(this);
        ImGuiImplDX12InitInfo initInfo = new()
        {
            Device = (HexaD3D12.ID3D12Device*)_device.NativePointer,
            CommandQueue = (HexaD3D12.ID3D12CommandQueue*)_queue.NativePointer,
            NumFramesInFlight = BufferCount,
            RTVFormat = (int)BackBufferFormat,
            DSVFormat = (int)Format.Unknown,
            UserData = (void*)GCHandle.ToIntPtr(_selfHandle),
            SrvDescriptorHeap = (HexaD3D12.ID3D12DescriptorHeap*)_srvHeap.NativePointer,
            SrvDescriptorAllocFn = (void*)(delegate* unmanaged[Cdecl]<ImGuiImplDX12InitInfo*, HexaD3D12.D3D12CpuDescriptorHandle*, HexaD3D12.D3D12GpuDescriptorHandle*, void>)&AllocateSrvDescriptor,
            SrvDescriptorFreeFn = (void*)(delegate* unmanaged[Cdecl]<ImGuiImplDX12InitInfo*, HexaD3D12.D3D12CpuDescriptorHandle, HexaD3D12.D3D12GpuDescriptorHandle, void>)&FreeSrvDescriptor
        };

        if (!ImGuiImplD3D12.Init(ref initInfo))
        {
            ImGuiImplWin32.Shutdown();
            throw new InvalidOperationException("ImGui Direct3D 12 backend 初始化失败。");
        }
        _imguiInitialized = true;
    }

    private static void ApplyStyle()
    {
        // Exact counterpart of CheryTools ImGuiController.ClampPanelStyle().
        // The default Dear ImGui spacing and color palette are intentionally preserved.
        ImGuiStylePtr style = ImGui.GetStyle();
        style.WindowRounding = 6f;
        style.ChildRounding = 5f;
        style.PopupRounding = 5f;
        style.FrameRounding = 4f;
        style.GrabRounding = 4f;
        style.ScrollbarRounding = 6f;
        style.TabRounding = 4f;
    }

    private void LoadExampleBackground()
    {
        using Stream imageStream = OpenEmbeddedResource("CheryFramework.Preview.Resources.mian.png");
        using System.Drawing.Bitmap source = new(imageStream);
        using System.Drawing.Bitmap bitmap = new(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        }

        ResourceDescription textureDescription = ResourceDescription.Texture2D(
            Format.B8G8R8A8_UNorm,
            (uint)bitmap.Width,
            (uint)bitmap.Height,
            mipLevels: 1);

        ID3D12Resource texture = _device.CreateCommittedResource(
            HeapType.Default,
            textureDescription,
            ResourceStates.CopyDest);

        PlacedSubresourceFootPrint[] layouts = new PlacedSubresourceFootPrint[1];
        uint[] rowCounts = new uint[1];
        ulong[] rowSizes = new ulong[1];
        _device.GetCopyableFootprints(textureDescription, 0, 1, 0, layouts, rowCounts, rowSizes, out ulong uploadSize);

        using ID3D12Resource upload = _device.CreateCommittedResource(
            HeapType.Upload,
            ResourceDescription.Buffer(uploadSize),
            ResourceStates.GenericRead);

        Rectangle rectangle = new(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte* destination = upload.Map<byte>(0);
            int sourcePitch = data.Stride;
            int rowBytes = bitmap.Width * 4;
            int destinationPitch = (int)layouts[0].Footprint.RowPitch;
            byte* sourceBase = (byte*)data.Scan0;

            for (int y = 0; y < bitmap.Height; y++)
            {
                byte* sourceRow = sourcePitch >= 0
                    ? sourceBase + y * sourcePitch
                    : sourceBase + (bitmap.Height - 1 - y) * -sourcePitch;
                byte* destinationRow = destination + y * destinationPitch;
                Buffer.MemoryCopy(sourceRow, destinationRow, destinationPitch, rowBytes);
            }
            upload.Unmap(0);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        _commandAllocator.Reset();
        _commandList.Reset(_commandAllocator);
        TextureCopyLocation destinationLocation = new(texture, 0);
        TextureCopyLocation sourceLocation = new(upload, layouts[0]);
        _commandList.CopyTextureRegion(destinationLocation, 0, 0, 0, sourceLocation, null);
        _commandList.ResourceBarrierTransition(texture, ResourceStates.CopyDest, ResourceStates.PixelShaderResource);
        _commandList.Close();
        _queue.ExecuteCommandList(_commandList);
        WaitForGpu();

        if (!TryAllocateSrvDescriptor(out int descriptorIndex, out CpuDescriptorHandle cpuHandle, out GpuDescriptorHandle gpuHandle))
        {
            texture.Dispose();
            throw new InvalidOperationException("没有可用于示例背景图片的 D3D12 SRV 描述符。");
        }

        ShaderResourceViewDescription view = new()
        {
            Shader4ComponentMapping = ShaderComponentMapping.Default,
            Format = Format.B8G8R8A8_UNorm,
            ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Texture2D,
            Texture2D = new Texture2DShaderResourceView
            {
                MostDetailedMip = 0,
                MipLevels = 1,
                PlaneSlice = 0,
                ResourceMinLODClamp = 0f
            }
        };
        _device.CreateShaderResourceView(texture, view, cpuHandle);

        _exampleBackgroundTexture = texture;
        _exampleBackgroundDescriptorIndex = descriptorIndex;
        PreviewPanel.SetExampleBackground(
            new ImTextureRef(null, new ImTextureID(gpuHandle.Ptr)),
            new Vector2(bitmap.Width, bitmap.Height));
    }

    private void CreateRenderTargets()
    {
        CpuDescriptorHandle handle = _rtvHeap.GetCPUDescriptorHandleForHeapStart();
        for (uint i = 0; i < BufferCount; i++)
        {
            _renderTargets[i] = _swapChain.GetBuffer<ID3D12Resource>(i);
            _device.CreateRenderTargetView(_renderTargets[i], null, handle);
            handle += (int)_rtvDescriptorSize;
        }
    }

    private void ReleaseRenderTargets()
    {
        for (int i = 0; i < _renderTargets.Length; i++)
        {
            _renderTargets[i]?.Dispose();
            _renderTargets[i] = null;
        }
    }

    private void WaitForGpu()
    {
        if (_queue == null || _fence == null) return;
        ulong value = ++_fenceValue;
        _queue.Signal(_fence, value);
        if (_fence.CompletedValue < value)
        {
            _fence.SetEventOnCompletion(value, _fenceEvent);
            _fenceEvent.WaitOne();
        }
    }

    private static ID3D12Device CreateDevice(IDXGIFactory4 factory)
    {
        for (uint index = 0; factory.EnumAdapters1(index, out IDXGIAdapter1? adapter).Success; index++)
        {
            using (adapter)
            {
                if ((adapter.Description1.Flags & AdapterFlags.Software) != AdapterFlags.None) continue;
                if (D3D12CreateDevice(adapter, FeatureLevel.Level_11_0, out ID3D12Device? device).Success && device != null)
                {
                    return device;
                }
            }
        }

        factory.EnumWarpAdapter(out IDXGIAdapter? warpAdapter).CheckError();
        using (warpAdapter)
        {
            D3D12CreateDevice(warpAdapter, FeatureLevel.Level_11_0, out ID3D12Device? device).CheckError();
            return device ?? throw new InvalidOperationException("无法创建 Direct3D 12 设备。");
        }
    }

    private static string ExtractFontToTemporaryFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"CheryFramework.Preview.MiSans.{Environment.ProcessId}.ttf");
        using Stream source = OpenEmbeddedResource("CheryFramework.Preview.Resources.MiSans-Bold.ttf");
        using FileStream destination = new(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        source.CopyTo(destination);
        return path;
    }

    private static Stream OpenEmbeddedResource(string name)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("缺少嵌入资源：" + name);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void AllocateSrvDescriptor(
        ImGuiImplDX12InitInfo* info,
        HexaD3D12.D3D12CpuDescriptorHandle* cpuOut,
        HexaD3D12.D3D12GpuDescriptorHandle* gpuOut)
    {
        D3D12PreviewRenderer? owner = GetOwner(info);
        if (owner == null)
        {
            *cpuOut = default;
            *gpuOut = default;
            return;
        }

        if (owner.TryAllocateSrvDescriptor(out _, out CpuDescriptorHandle cpu, out GpuDescriptorHandle gpu))
        {
            *cpuOut = new HexaD3D12.D3D12CpuDescriptorHandle(cpu.Ptr);
            *gpuOut = new HexaD3D12.D3D12GpuDescriptorHandle(gpu.Ptr);
            return;
        }

        *cpuOut = default;
        *gpuOut = default;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void FreeSrvDescriptor(
        ImGuiImplDX12InitInfo* info,
        HexaD3D12.D3D12CpuDescriptorHandle cpu,
        HexaD3D12.D3D12GpuDescriptorHandle gpu)
    {
        D3D12PreviewRenderer? owner = GetOwner(info);
        if (owner == null) return;
        ulong start = owner._srvHeap.GetGPUDescriptorHandleForHeapStart().Ptr;
        if (gpu.Ptr < start || owner._srvDescriptorSize == 0) return;
        ulong offset = gpu.Ptr - start;
        int index = (int)(offset / owner._srvDescriptorSize);
        if ((uint)index < (uint)owner._srvDescriptorsUsed.Length) owner._srvDescriptorsUsed[index] = false;
    }

    private static D3D12PreviewRenderer? GetOwner(ImGuiImplDX12InitInfo* info)
    {
        if (info == null || info->UserData == null) return null;
        GCHandle handle = GCHandle.FromIntPtr((nint)info->UserData);
        return handle.Target as D3D12PreviewRenderer;
    }

    private bool TryAllocateSrvDescriptor(out int index, out CpuDescriptorHandle cpu, out GpuDescriptorHandle gpu)
    {
        for (int i = 0; i < _srvDescriptorsUsed.Length; i++)
        {
            if (_srvDescriptorsUsed[i]) continue;
            _srvDescriptorsUsed[i] = true;
            index = i;
            cpu = new CpuDescriptorHandle(_srvHeap.GetCPUDescriptorHandleForHeapStart(), i, _srvDescriptorSize);
            gpu = new GpuDescriptorHandle(_srvHeap.GetGPUDescriptorHandleForHeapStart(), i, _srvDescriptorSize);
            return true;
        }

        index = -1;
        cpu = default;
        gpu = default;
        return false;
    }

}
