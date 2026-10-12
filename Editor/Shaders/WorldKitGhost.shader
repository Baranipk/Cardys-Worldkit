// World Kit önizleme (hayalet) shader'ı: yarı saydam, sabit yönlü basit ışık + kenar parlaması.
// Işık hesabı shader'ın içinde olduğu için sahnenin ışığından bağımsız ve her render pipeline'da okunur.
// LightMode etiketi yok: URP bunu "SRPDefaultUnlit" olarak çizer, Built-in normal çizer.
Shader "Hidden/WorldKit/Ghost"
{
    Properties
    {
        _Color ("Color", Color) = (0.25, 0.65, 1, 0.55)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 view : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 l = normalize(float3(0.35, 1.0, 0.2));
                float diffuse = saturate(dot(n, l)) * 0.55 + 0.45;
                float rim = pow(1.0 - saturate(dot(n, normalize(i.view))), 2.0) * 0.45;
                fixed4 c = _Color;
                c.rgb = c.rgb * diffuse + rim;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
