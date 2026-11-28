// Purr-tales — feedback de dash del gato (estilo Celeste) adaptado a URP 2D (HLSL)
// _DashListo = 1 -> ojos y medallón dorados con pulso de brillo (dash disponible)
// _DashListo = 0 -> ojos y medallón azules apagados (dash gastado)
// Compatible con Universal Render Pipeline 2D, SpriteRenderer.flipX, SRP Batcher y Linear/Gamma.
Shader "PurrTales/GatoDashColor"
{
    Properties
    {
        [MainTexture] [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
        _DashListo ("Dash listo (0/1)", Range(0, 1)) = 1
        _Brillo ("Pulso de brillo cuando esta listo", Range(0, 1)) = 0.25
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 color      : COLOR;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color       : COLOR;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _DashListo;
            float _Brillo;
        CBUFFER_END

        Varyings vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            SetUpSpriteInstanceProperties();
            input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            output.color = (half4)(input.color * _Color * unity_SpriteColor);
            return output;
        }

        bool IsApproxColor(float3 c, float3 target)
        {
            return all(abs(c - target) < 0.025);
        }

        half4 frag(Varyings input) : SV_Target
        {
            float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

            // Comparar y reemplazar en sRGB para exactitud tanto en Linear como en Gamma.
            #ifndef UNITY_COLORSPACE_GAMMA
                float3 srgb = LinearToSRGB(saturate(tex.rgb));
            #else
                float3 srgb = tex.rgb;
            #endif

            // Paleta dorada original (ojos y medallón)
            const float3 g1 = float3(243.0 / 255.0, 197.0 / 255.0,  60.0 / 255.0); // #F3C53C
            const float3 g2 = float3(197.0 / 255.0, 134.0 / 255.0,  51.0 / 255.0); // #C58633
            const float3 g3 = float3(138.0 / 255.0,  90.0 / 255.0,  48.0 / 255.0); // #8A5A30
            const float3 g4 = float3(231.0 / 255.0, 218.0 / 255.0, 139.0 / 255.0); // #E7DA8B

            bool isG1 = IsApproxColor(srgb, g1);
            bool isG2 = IsApproxColor(srgb, g2);
            bool isG3 = IsApproxColor(srgb, g3);
            bool isG4 = IsApproxColor(srgb, g4);

            if (isG1 || isG2 || isG3 || isG4)
            {
                if (_DashListo < 0.5)
                {
                    // Colores azules apagados de "dash gastado"
                    if (isG1)      srgb = float3(163.0 / 255.0, 180.0 / 255.0, 214.0 / 255.0); // #A3B4D6
                    else if (isG2) srgb = float3(100.0 / 255.0, 120.0 / 255.0, 168.0 / 255.0); // #6478A8
                    else if (isG3) srgb = float3( 58.0 / 255.0,  79.0 / 255.0, 130.0 / 255.0); // #3A4F82
                    else           srgb = float3(220.0 / 255.0, 214.0 / 255.0, 232.0 / 255.0); // #DCD6E8
                }
                else
                {
                    // Pulso suave de brillo dorado cuando el dash está disponible
                    float pulse = _Brillo * (0.5 + 0.5 * sin(_Time.y * 6.0)) * 0.35;
                    srgb = saturate(srgb + pulse);
                }

                #ifndef UNITY_COLORSPACE_GAMMA
                    tex.rgb = SRGBToLinear(srgb);
                #else
                    tex.rgb = srgb;
                #endif
            }

            return (half4)tex * input.color;
        }
        ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" "Queue" = "Transparent" "RenderType" = "Transparent" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
