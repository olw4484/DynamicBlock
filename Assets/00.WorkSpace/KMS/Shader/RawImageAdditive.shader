Shader "UI/RawImageAlpha"
{
    Properties { _MainTex("Texture",2D)="white"{} }
    SubShader{
        Tags{ "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGBA
        Pass{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST;
            struct app{ float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f{ float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(app v){ v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv,_MainTex); return o; }
            fixed4 frag(v2f i):SV_Target{ return tex2D(_MainTex,i.uv); }
            ENDCG
        }
    }
}