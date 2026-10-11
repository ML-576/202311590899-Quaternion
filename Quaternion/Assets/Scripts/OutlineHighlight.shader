// 选中素材的黄色外轮廓描边着色器（反壳法 Inverted Hull）：
// 顶点沿法线外扩形成略大的外壳，只渲染背面（Cull Front），
// 物体本体会遮挡外壳内部，仅在轮廓边缘露出一圈颜色。
Shader "Custom/OutlineHighlight"
{
    Properties
    {
        _OutlineColor ("描边颜色", Color) = (1, 0.85, 0, 1)
        _OutlineThickness ("描边宽度", Range(0.001, 0.1)) = 0.025
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }

        Pass
        {
            Cull Front
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _OutlineColor;
            float _OutlineThickness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                v.vertex.xyz += v.normal * _OutlineThickness;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
    Fallback Off
}
