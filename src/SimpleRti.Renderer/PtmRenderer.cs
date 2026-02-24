using System.Runtime.InteropServices;
using Avalonia.OpenGL;
using SimpleRti.Ptm;
using static Avalonia.OpenGL.GlConsts;
using static SimpleRti.Renderer.GlConstants;

namespace SimpleRti.Renderer;

public sealed class PtmRenderer : IDisposable
{
    private int _shaderProgram;
    private int _vao, _vbo;

    // 6 coefficient textures (one per polynomial term a0..a5)
    // For LRGB: each stores (L, L, L, 0) — luminance replicated to RGB
    // For RGB: each stores (R, G, B, 0) — per-channel coefficients
    private readonly int[] _coeffTextures = new int[6];
    private int _colorTex; // LRGB: stored RGB color. RGB: white 1x1 placeholder.

    private int _uLightDir, _uRenderMode, _uSpecularExponent, _uDiffuseGain, _uIsLrgb;
    private readonly int[] _uCoeffs = new int[6];
    private int _uColorTex;

    private int _imageWidth, _imageHeight;
    private bool _initialized;
    private bool _isGles;
    private PtmFormat _format;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlUniform1i(int location, int v0);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlUniform1f(int location, float v0);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlUniform2f(int location, float v0, float v1);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr GlGetString(int name);

    private GlUniform1i? _uniform1i;
    private GlUniform1f? _uniform1f;
    private GlUniform2f? _uniform2f;

    private static T? LoadProc<T>(GlInterface gl, string name) where T : Delegate
    {
        var ptr = gl.GetProcAddress(name);
        return ptr != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<T>(ptr) : null;
    }

    public void Init(GlInterface gl, PtmFile ptm)
    {
        if (_initialized) return;

        _imageWidth = ptm.Header.Width;
        _imageHeight = ptm.Header.Height;
        _format = ptm.Header.Format;

        _uniform1i = LoadProc<GlUniform1i>(gl, "glUniform1i");
        _uniform1f = LoadProc<GlUniform1f>(gl, "glUniform1f");
        _uniform2f = LoadProc<GlUniform2f>(gl, "glUniform2f");

        var getString = LoadProc<GlGetString>(gl, "glGetString");
        if (getString != null)
        {
            var ptr = getString(GL_VERSION);
            if (ptr != IntPtr.Zero)
            {
                var version = Marshal.PtrToStringAnsi(ptr);
                _isGles = version?.Contains("OpenGL ES") == true;
            }
        }

        CompileShaders(gl);
        CreateQuad(gl);
        UploadTextures(gl, ptm);

        _initialized = true;
    }

    public void Render(GlInterface gl, int fbo, int viewportWidth, int viewportHeight,
        float lightU, float lightV, RenderMode mode, float specularExponent, float diffuseGain)
    {
        if (!_initialized) return;

        gl.BindFramebuffer(GL_FRAMEBUFFER, fbo);
        gl.Viewport(0, 0, viewportWidth, viewportHeight);
        gl.ClearColor(0.1f, 0.1f, 0.1f, 1f);
        gl.Clear(GL_COLOR_BUFFER_BIT);

        gl.UseProgram(_shaderProgram);

        // Bind 6 coefficient textures
        for (int i = 0; i < 6; i++)
        {
            gl.ActiveTexture(GL_TEXTURE0 + i);
            gl.BindTexture(GL_TEXTURE_2D, _coeffTextures[i]);
            _uniform1i?.Invoke(_uCoeffs[i], i);
        }

        // Bind color texture
        gl.ActiveTexture(GL_TEXTURE0 + 6);
        gl.BindTexture(GL_TEXTURE_2D, _colorTex);
        _uniform1i?.Invoke(_uColorTex, 6);

        // Set uniforms
        _uniform2f?.Invoke(_uLightDir, lightU, lightV);
        _uniform1i?.Invoke(_uRenderMode, (int)mode);
        _uniform1f?.Invoke(_uSpecularExponent, specularExponent);
        _uniform1f?.Invoke(_uDiffuseGain, diffuseGain);
        _uniform1i?.Invoke(_uIsLrgb, _format == PtmFormat.Lrgb ? 1 : 0);

        gl.BindVertexArray(_vao);
        gl.DrawArrays(GL_TRIANGLES, 0, new IntPtr(6));
    }

    public void Deinit(GlInterface gl)
    {
        if (!_initialized) return;

        gl.DeleteProgram(_shaderProgram);
        gl.DeleteBuffer(_vbo);
        gl.DeleteVertexArray(_vao);
        for (int i = 0; i < 6; i++)
            gl.DeleteTexture(_coeffTextures[i]);
        gl.DeleteTexture(_colorTex);

        _initialized = false;
    }

    public void Dispose() { }

    private void CompileShaders(GlInterface gl)
    {
        int vs = gl.CreateShader(GL_VERTEX_SHADER);
        string? vsErr = gl.CompileShaderAndGetError(vs, ShaderSources.GetVertex(_isGles));
        if (!string.IsNullOrEmpty(vsErr))
            throw new InvalidOperationException($"Vertex shader error: {vsErr}");

        int fs = gl.CreateShader(GL_FRAGMENT_SHADER);
        string? fsErr = gl.CompileShaderAndGetError(fs, ShaderSources.GetFragment(_isGles));
        if (!string.IsNullOrEmpty(fsErr))
            throw new InvalidOperationException($"Fragment shader error: {fsErr}");

        _shaderProgram = gl.CreateProgram();
        gl.AttachShader(_shaderProgram, vs);
        gl.AttachShader(_shaderProgram, fs);

        gl.BindAttribLocationString(_shaderProgram, 0, "aPos");
        gl.BindAttribLocationString(_shaderProgram, 1, "aTexCoord");

        string? linkErr = gl.LinkProgramAndGetError(_shaderProgram);
        if (!string.IsNullOrEmpty(linkErr))
            throw new InvalidOperationException($"Shader link error: {linkErr}");

        _uLightDir = gl.GetUniformLocationString(_shaderProgram, "uLightDir");
        _uRenderMode = gl.GetUniformLocationString(_shaderProgram, "uRenderMode");
        _uSpecularExponent = gl.GetUniformLocationString(_shaderProgram, "uSpecularExponent");
        _uDiffuseGain = gl.GetUniformLocationString(_shaderProgram, "uDiffuseGain");
        _uIsLrgb = gl.GetUniformLocationString(_shaderProgram, "uIsLrgb");
        _uColorTex = gl.GetUniformLocationString(_shaderProgram, "uColorTex");

        for (int i = 0; i < 6; i++)
            _uCoeffs[i] = gl.GetUniformLocationString(_shaderProgram, $"uCoeff{i}");
    }

    private void CreateQuad(GlInterface gl)
    {
        float[] vertices =
        [
            -1f, -1f,  0f, 0f,
             1f, -1f,  1f, 0f,
             1f,  1f,  1f, 1f,
            -1f, -1f,  0f, 0f,
             1f,  1f,  1f, 1f,
            -1f,  1f,  0f, 1f,
        ];

        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);

        _vbo = gl.GenBuffer();
        gl.BindBuffer(GL_ARRAY_BUFFER, _vbo);

        var handle = GCHandle.Alloc(vertices, GCHandleType.Pinned);
        try
        {
            gl.BufferData(GL_ARRAY_BUFFER, new IntPtr(vertices.Length * sizeof(float)),
                handle.AddrOfPinnedObject(), GL_STATIC_DRAW);
        }
        finally { handle.Free(); }

        int stride = 4 * sizeof(float);
        gl.VertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, stride, IntPtr.Zero);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(1, 2, GL_FLOAT, GL_FALSE, stride, new IntPtr(2 * sizeof(float)));
        gl.EnableVertexAttribArray(1);
    }

    private unsafe void UploadTextures(GlInterface gl, PtmFile ptm)
    {
        int pixelCount = _imageWidth * _imageHeight;

        if (ptm.Header.Format == PtmFormat.Lrgb)
        {
            // LRGB: 6 luminance coefficient planes → replicate to RGB channels
            for (int c = 0; c < 6; c++)
            {
                float[] rgba = new float[pixelCount * 4];
                for (int i = 0; i < pixelCount; i++)
                {
                    float val = ptm.Coefficients[c][i];
                    rgba[i * 4 + 0] = val;
                    rgba[i * 4 + 1] = val;
                    rgba[i * 4 + 2] = val;
                    rgba[i * 4 + 3] = 0f;
                }
                _coeffTextures[c] = CreateFloatTexture(gl, _imageWidth, _imageHeight, rgba);
            }

            // Upload the stored RGB color texture
            _colorTex = gl.GenTexture();
            gl.BindTexture(GL_TEXTURE_2D, _colorTex);
            fixed (byte* ptr = ptm.Rgb)
            {
                gl.TexImage2D(GL_TEXTURE_2D, 0, GL_RGB, _imageWidth, _imageHeight,
                    0, GL_RGB, GL_UNSIGNED_BYTE, new IntPtr(ptr));
            }
            gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
            gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
            gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
            gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
        }
        else
        {
            // RGB: 18 planes → coefficients[coeffIdx*3 + channel]
            // Pack into 6 RGBA32F textures: R=red_coeff, G=green_coeff, B=blue_coeff
            for (int c = 0; c < 6; c++)
            {
                float[] rgba = new float[pixelCount * 4];
                for (int i = 0; i < pixelCount; i++)
                {
                    rgba[i * 4 + 0] = ptm.Coefficients[c * 3 + 0][i]; // R channel
                    rgba[i * 4 + 1] = ptm.Coefficients[c * 3 + 1][i]; // G channel
                    rgba[i * 4 + 2] = ptm.Coefficients[c * 3 + 2][i]; // B channel
                    rgba[i * 4 + 3] = 0f;
                }
                _coeffTextures[c] = CreateFloatTexture(gl, _imageWidth, _imageHeight, rgba);
            }

            // White placeholder color texture (not used for RGB, but keep shader simple)
            byte[] white = [255, 255, 255, 255];
            _colorTex = gl.GenTexture();
            gl.BindTexture(GL_TEXTURE_2D, _colorTex);
            fixed (byte* ptr = white)
            {
                gl.TexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, 1, 1,
                    0, GL_RGBA, GL_UNSIGNED_BYTE, new IntPtr(ptr));
            }
            gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
            gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
        }
    }

    private unsafe int CreateFloatTexture(GlInterface gl, int width, int height, float[] data)
    {
        int tex = gl.GenTexture();
        gl.BindTexture(GL_TEXTURE_2D, tex);

        fixed (float* ptr = data)
        {
            gl.TexImage2D(GL_TEXTURE_2D, 0, GL_RGBA32F, width, height,
                0, GL_RGBA, GL_FLOAT, new IntPtr(ptr));
        }

        gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
        gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
        gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
        gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);

        return tex;
    }
}
