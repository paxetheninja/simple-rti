namespace SimpleRti.Renderer;

public static class ShaderSources
{
    public static string GetVertex(bool isGles)
    {
        if (isGles) return Vertex;
        return Vertex
            .Replace("#version 300 es", "#version 330")
            .Replace("precision highp float;", "");
    }

    public static string GetFragment(bool isGles)
    {
        if (isGles) return Fragment;
        return Fragment
            .Replace("#version 300 es", "#version 330")
            .Replace("precision highp float;", "")
            .Replace("precision highp sampler2D;", "");
    }

    public const string Vertex = """
        #version 300 es
        precision highp float;
        in vec2 aPos;
        in vec2 aTexCoord;
        out vec2 vTexCoord;

        void main() {
            gl_Position = vec4(aPos, 0.0, 1.0);
            vTexCoord = aTexCoord;
        }
        """;

    public const string Fragment = """
        #version 300 es
        precision highp float;
        precision highp sampler2D;
        in vec2 vTexCoord;
        out vec4 fragColor;

        // 6 coefficient textures: each has RGB per-channel coefficients
        // For LRGB: R=G=B=luminance. For RGB: R,G,B are independent.
        uniform sampler2D uCoeff0;  // a0 coefficients
        uniform sampler2D uCoeff1;  // a1 coefficients
        uniform sampler2D uCoeff2;  // a2 coefficients
        uniform sampler2D uCoeff3;  // a3 coefficients
        uniform sampler2D uCoeff4;  // a4 coefficients
        uniform sampler2D uCoeff5;  // a5 coefficients
        uniform sampler2D uColorTex;  // LRGB: stored RGB color. RGB: white.

        uniform vec2 uLightDir;
        uniform int uRenderMode;     // 0=default, 1=specular, 2=diffuse gain, 3=normal map
        uniform float uSpecularExponent;
        uniform float uDiffuseGain;
        uniform int uIsLrgb;         // 1=LRGB format, 0=RGB format

        // Evaluate PTM polynomial per-channel (vec3)
        vec3 evaluatePtm(float u, float v, vec3 a0, vec3 a1, vec3 a2, vec3 a3, vec3 a4, vec3 a5) {
            return a0 * u * u
                 + a1 * v * v
                 + a2 * u * v
                 + a3 * u
                 + a4 * v
                 + a5;
        }

        // Extract surface normal from luminance channel (uses .r component)
        vec3 extractNormal(float a0, float a1, float a2, float a3, float a4) {
            float dLdu = 2.0 * a0 * uLightDir.x + a2 * uLightDir.y + a3;
            float dLdv = 2.0 * a1 * uLightDir.y + a2 * uLightDir.x + a4;
            return normalize(vec3(-dLdu, -dLdv, 1.0));
        }

        void main() {
            vec3 a0 = texture(uCoeff0, vTexCoord).rgb;
            vec3 a1 = texture(uCoeff1, vTexCoord).rgb;
            vec3 a2 = texture(uCoeff2, vTexCoord).rgb;
            vec3 a3 = texture(uCoeff3, vTexCoord).rgb;
            vec3 a4 = texture(uCoeff4, vTexCoord).rgb;
            vec3 a5 = texture(uCoeff5, vTexCoord).rgb;
            vec3 storedColor = texture(uColorTex, vTexCoord).rgb;

            float u = uLightDir.x;
            float v = uLightDir.y;

            if (uRenderMode == 0) {
                // Default relighting
                vec3 result = evaluatePtm(u, v, a0, a1, a2, a3, a4, a5);
                result = clamp(result / 255.0, 0.0, 1.0);
                if (uIsLrgb == 1) {
                    result = result * storedColor;
                }
                fragColor = vec4(result, 1.0);
            }
            else if (uRenderMode == 1) {
                // Specular enhancement
                vec3 result = evaluatePtm(u, v, a0, a1, a2, a3, a4, a5);
                result = clamp(result / 255.0, 0.0, 1.0);
                if (uIsLrgb == 1) {
                    result = result * storedColor;
                }
                vec3 normal = extractNormal(a0.r, a1.r, a2.r, a3.r, a4.r);
                vec3 lightDir = normalize(vec3(u, v, sqrt(max(1.0 - u*u - v*v, 0.0))));
                vec3 viewDir = vec3(0.0, 0.0, 1.0);
                vec3 halfVec = normalize(lightDir + viewDir);
                float spec = pow(max(dot(normal, halfVec), 0.0), uSpecularExponent);
                vec3 color = result * 0.8 + vec3(spec) * 0.5;
                fragColor = vec4(clamp(color, 0.0, 1.0), 1.0);
            }
            else if (uRenderMode == 2) {
                // Diffuse gain
                float ug = u * uDiffuseGain;
                float vg = v * uDiffuseGain;
                vec3 result = evaluatePtm(ug, vg, a0, a1, a2, a3, a4, a5);
                result = clamp(result / 255.0, 0.0, 1.0);
                if (uIsLrgb == 1) {
                    result = result * storedColor;
                }
                fragColor = vec4(result, 1.0);
            }
            else if (uRenderMode == 3) {
                // Normal map visualization
                vec3 normal = extractNormal(a0.r, a1.r, a2.r, a3.r, a4.r);
                fragColor = vec4(normal * 0.5 + 0.5, 1.0);
            }
            else {
                fragColor = vec4(1.0, 0.0, 1.0, 1.0);
            }
        }
        """;
}
