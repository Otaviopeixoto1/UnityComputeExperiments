Shader "Unlit/MCTerrainShader"
{
    /*
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }*/
    SubShader
    {
        Pass
        {
            Cull Off 
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"
            
            

            struct Interpolators
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR0;
            };
            

            struct Vert 
            {
                float4 position;
                float4 normal;
            };

            StructuredBuffer<Vert> vertexBuffer;

            Interpolators vert(uint svVertexID: SV_VertexID, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                Interpolators o;
                uint cmdID = GetCommandID(0);
                uint instanceID = GetIndirectInstanceID(svInstanceID);
                float3 pos = vertexBuffer[GetIndirectVertexID(svVertexID)].position;
                o.pos = mul(UNITY_MATRIX_VP, pos);
                //o.color = float4(cmdID & 1 ? 0.0f : 1.0f, cmdID & 1 ? 1.0f : 0.0f, instanceID / float(GetIndirectInstanceCount()), 0.0f);
                o.color = float4(1.0, 0.0f, 0.0f, 0.0f);
                return o;
            }

            float4 frag(Interpolators i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
