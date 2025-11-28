// See https://aka.ms/new-console-template for more information
// Console.WriteLine("Hello, World!");


using System;
using System.Numerics;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

class Program
{
    static IWindow _window;
    static GL _gl;

    static uint _vao, _vbo, _shaderProgram;
    
    // 顶点（位置 + 颜色）
    // 位置: (x,y)  颜色: (r,g,b)
    private static readonly float[] _vertices =
    {
        // 位置         // 颜色
         0.0f,  0.6f,   1.0f, 0.0f, 0.0f, // 顶点 A：红
        -0.6f, -0.6f,   0.0f, 1.0f, 0.0f, // 顶点 B：绿
         0.6f, -0.6f,   0.0f, 0.0f, 1.0f  // 顶点 C：蓝
    };

    // 顶点着色器（把位置直接传到裁剪空间，并把颜色传到片段着色器）
    private const string VertexShader = @"#version 330 core
layout(location = 0) in vec2 inPos;
layout(location = 1) in vec3 inColor;
out vec3 vColor;
void main()
{
    gl_Position = vec4(inPos.xy, 0.0, 1.0);
    vColor = inColor;
}";

    // 片段着色器（输出插值后的颜色）
    private const string FragmentShader = @"#version 330 core
in vec3 vColor;
out vec4 FragColor;
void main()
{
    FragColor = vec4(vColor, 1.0);
}";

    static void Main(string[] args)
    {
        var options = WindowOptions.Default;
        options.Size = new Silk.NET.Maths.Vector2D<int>(800, 600);
        options.Title = "Silk.NET OpenGL Example";

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Closing += OnClose;
        _window.Run();
    }


private static void OnLoad()
    {
        _gl = GL.GetApi(_window);

        // 设置视口与背景色
        var size = _window.Size;
        _gl.Viewport(0, 0, (uint)size.X, (uint)size.Y);
        _gl.ClearColor(0.12f, 0.12f, 0.15f, 1.0f);

        // 创建 VAO/VBO
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* v = _vertices)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer,
                    (nuint)(_vertices.Length * sizeof(float)),
                    v, BufferUsageARB.StaticDraw);
            }
        }

        // 顶点属性：位置 (location=0, vec2) 和 颜色 (location=1, vec3)
        const int stride = (2 + 3) * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));

        // 编译与链接着色器
        _shaderProgram = CreateProgram(VertexShader, FragmentShader);

        Console.WriteLine("OpenGL info:");
        Console.WriteLine($"  Vendor:   {_gl.GetStringS(StringName.Vendor)}");
        Console.WriteLine($"  Renderer: {_gl.GetStringS(StringName.Renderer)}");
        Console.WriteLine($"  Version:  {_gl.GetStringS(StringName.Version)}");
    }

    private static void OnUpdate(double dt)
    {
        // 每秒更新标题显示帧率
        _window.Title = $"Silk.NET OpenGL Demo  |  FPS: {(1.0 / dt):F1}";
    }

    private static void OnRender(double dt)
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _gl.UseProgram(_shaderProgram);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
    }

    private static void OnClose()
    {
        // 释放 GPU 资源
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteProgram(_shaderProgram);
    }

    private static uint CreateProgram(string vsSrc, string fsSrc)
    {
        uint vs = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vs, vsSrc);
        _gl.CompileShader(vs);
        CheckShader(vs, "Vertex");

        uint fs = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fs, fsSrc);
        _gl.CompileShader(fs);
        CheckShader(fs, "Fragment");

        uint prog = _gl.CreateProgram();
        _gl.AttachShader(prog, vs);
        _gl.AttachShader(prog, fs);
        _gl.LinkProgram(prog);
        CheckProgram(prog);

        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
        return prog;
    }

    private static void CheckShader(uint shader, string name)
    {
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string log = _gl.GetShaderInfoLog(shader);
            throw new Exception($"{name} shader compile error:\n{log}");
        }
    }

    private static void CheckProgram(uint program)
    {
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0)
        {
            string log = _gl.GetProgramInfoLog(program);
            throw new Exception($"Program link error:\n{log}");
        }
    }

}