using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.D3DCompiler.Compiler;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace SilkWinform
{
    public partial class Form1 : Form
    {
        // D3D11 相关
        private IDXGIFactory2 _factory;
        private ID3D11Device _device;
        private ID3D11DeviceContext _context;
        private IDXGISwapChain1 _swapChain;
        private ID3D11RenderTargetView _rtv;
        private ID3D11Texture2D _depthTex;
        private ID3D11DepthStencilView _dsv;

        // 资源：顶点/索引/常量缓冲/着色器
        private ID3D11Buffer _vb, _ib, _cbMvp;
        private ID3D11VertexShader _vs;
        private ID3D11PixelShader _ps;
        private ID3D11InputLayout _inputLayout;

        private DateTime _t0 = DateTime.Now;

        public Form1()
        {
            Text = "D3D11 WinForms Rotating Cube";
            ClientSize = new System.Drawing.Size(1024, 600);
            StartPosition = FormStartPosition.CenterScreen;

            // 初始化 D3D
            InitializeD3D();
            CreateSceneResources();

            // 用 Idle 事件驱动渲染循环（也可以用 Timer）
            Application.Idle += (_, __) => { Render(); };
            this.Resize += (_, __) => ResizeSwapChain();
            this.FormClosed += (_, __) => Cleanup();
        }

        private void InitializeD3D()
        {
            _factory = CreateDXGIFactory2<IDXGIFactory2>(false);

            // 创建设备与上下文
            D3D11CreateDevice(
                null, // 让系统选适配器
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport, // WIC/SwapChain 需要
                new[] { FeatureLevel.Level_11_0 },
                out _device,
                out _context
            );

            // 创建交换链（基于 WinForms 窗口句柄）
            var desc = new SwapChainDescription1
            {
                Width = (uint)ClientSize.Width,
                Height = (uint)ClientSize.Height,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipDiscard,
            };

            using var hwnd = new Hwnd(Handle);
            var fsDesc = new SwapChainFullscreenDescription(); // 默认窗口模式
            _swapChain = _factory.CreateSwapChainForHwnd(_device, hwnd.Handle, desc, fsDesc, null);

            CreateTargets();
        }

        private void CreateTargets()
        {
            // RTV
            using var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            _rtv = _device.CreateRenderTargetView(backBuffer);

            // 深度纹理 + DSV
            var depthDesc = new Texture2DDescription
            {
                Width = (uint)ClientSize.Width,
                Height = (uint)ClientSize.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(1, 0),
                BindFlags = BindFlags.DepthStencil,
            };
            _depthTex = _device.CreateTexture2D(depthDesc);
            _dsv = _device.CreateDepthStencilView(_depthTex);

            // 设置视口
            _context.RSSetViewports(new Viewport[] { new Viewport(0, 0, (float)ClientSize.Width, (float)ClientSize.Height, 0.0f, 1.0f) });
        }

        private void ResizeSwapChain()
        {
            _rtv?.Dispose();
            _dsv?.Dispose();
            _depthTex?.Dispose();

            _swapChain.ResizeBuffers(0, (uint)ClientSize.Width, (uint)ClientSize.Height, Format.Unknown, SwapChainFlags.None);
            CreateTargets();
        }

        private struct Vertex
        {
            public Vector3 Position;
            public Vector3 Color;
            public Vertex(Vector3 p, Vector3 c) { Position = p; Color = c; }
        }

        private void CreateSceneResources()
        {
            // 顶点 & 索引（立方体）
            var verts = new[]
            {
                new Vertex(new Vector3(-0.5f,-0.5f, 0.5f), new Vector3(1,0,0)), // 前面
                new Vertex(new Vector3( 0.5f,-0.5f, 0.5f), new Vector3(0,1,0)),
                new Vertex(new Vector3( 0.5f, 0.5f, 0.5f), new Vector3(0,0,1)),
                new Vertex(new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(1,1,0)),
                new Vertex(new Vector3(-0.5f,-0.5f,-0.5f), new Vector3(1,0,1)), // 背面
                new Vertex(new Vector3( 0.5f,-0.5f,-0.5f), new Vector3(0,1,1)),
                new Vertex(new Vector3( 0.5f, 0.5f,-0.5f), new Vector3(1,1,1)),
                new Vertex(new Vector3(-0.5f, 0.5f,-0.5f), new Vector3(0.2f,0.8f,0.4f)),
            };

            uint[] indices =
            {
                0,1,2, 0,2,3, // 前
                5,4,7, 5,7,6, // 后
                4,0,3, 4,3,7, // 左
                1,5,6, 1,6,2, // 右
                4,5,1, 4,1,0, // 下
                3,2,6, 3,6,7  // 上
            };

            _vb = CreateBuffer(verts, BindFlags.VertexBuffer);
            _ib = CreateBuffer(indices, BindFlags.IndexBuffer);
            _cbMvp = CreateBuffer(new Matrix4x4(), BindFlags.ConstantBuffer);

            // 着色器：VS + PS（HLSL）
            string vsSrc = @"
cbuffer CBVS : register(b0)
{
    float4x4 uMVP;
};

struct VSIn { float3 Pos : POSITION; float3 Col : COLOR; };
struct VSOut { float4 Pos : SV_Position; float3 Col : COLOR; };

VSOut main(VSIn input)
{
    VSOut o;
    o.Pos = mul(uMVP, float4(input.Pos, 1.0));
    o.Col = input.Col;
    return o;
}";
            string psSrc = @"
struct PSIn { float4 Pos : SV_Position; float3 Col : COLOR; };
float4 main(PSIn input) : SV_Target
{
    return float4(input.Col, 1.0);
}";

            // 编译着色器（用 D3DCompile，Vortice.D3DCompiler 可选；这里为了简洁用 ShaderBytecode）
            var vsBytecode = Vortice.D3DCompiler.Compiler.Compile(vsSrc, "main",
                "vs_5_0");
            var psBytecode = Vortice.D3DCompiler.Compiler.Compile(psSrc, "main",
                "ps_5_0", vsSrc);

            _vs = _device.CreateVertexShader(vsBytecode);
            _ps = _device.CreatePixelShader(psBytecode);

            // 输入布局（POSITION + COLOR）
            var elements = new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElementDescription("COLOR",    0, Format.R32G32B32_Float, 12, 0),
            };
            _inputLayout = _device.CreateInputLayout(elements, vsBytecode);

            // 绑定静态状态
            _context.IASetInputLayout(_inputLayout);
            _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        }

        private ID3D11Buffer CreateBuffer<T>(T data, BindFlags bindFlags) where T : struct
        {
            var desc = new BufferDescription((uint)Marshal.SizeOf<T>(), bindFlags, ResourceUsage.Default);
            return _device.CreateBuffer(desc, new SubresourceData(data));
        }

        private ID3D11Buffer CreateBuffer<T>(T[] data, BindFlags bindFlags) where T : struct
        {
            int size = Marshal.SizeOf<T>() * data.Length;
            var desc = new BufferDescription((uint)size, bindFlags, ResourceUsage.Default);
            return _device.CreateBuffer(desc, new SubresourceData(data));
        }

        private void Render()
        {
            // 清屏
            _context.OMSetRenderTargets(_dsv, _rtv);
            _context.ClearRenderTargetView(_rtv, new Color4(26, 31, 36));
            _context.ClearDepthStencilView(_dsv, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);

            // 绑定缓冲
            _context.IASetVertexBuffers(0, new VertexBufferView(_vb, Marshal.SizeOf<Vertex>(), 0));
            _context.IASetIndexBuffer(_ib, Format.R32_UInt, 0);

            // 计算 MVP（随时间旋转）
            float t = (float)(DateTime.Now - _t0).TotalSeconds;
            var model = Matrix4x4.CreateRotationY(t) * Matrix4x4.CreateRotationX(t * 0.5f);
            float aspect = ClientSize.Width / (float)ClientSize.Height;
            var view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY);
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, aspect, 0.1f, 100f);
            var mvp = model * view * proj;

            _context.UpdateSubresource(ref mvp, _cbMvp);
            _context.VSSetConstantBuffers(0, _cbMvp);

            // 着色器
            _context.VSSetShader(_vs);
            _context.PSSetShader(_ps);

            // 绘制
            _context.DrawIndexed(36, 0, 0);

            _swapChain.Present(1, PresentFlags.None);
        }

        private void Cleanup()
        {
            _inputLayout?.Dispose();
            _vs?.Dispose();
            _ps?.Dispose();
            _vb?.Dispose();
            _ib?.Dispose();
            _cbMvp?.Dispose();
            _rtv?.Dispose();
            _dsv?.Dispose();
            _depthTex?.Dispose();
            _swapChain?.Dispose();
            _context?.Dispose();
            _device?.Dispose();
            _factory?.Dispose();
        }

        // 简单封装 HWND 句柄
        private readonly struct Hwnd : IDisposable
        {
            public readonly IntPtr Handle;
            public Hwnd(IntPtr h) => Handle = h;
            public void Dispose() { /* no-op */ }
        }
    }
}
