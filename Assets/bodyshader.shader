Shader "Unlit/bodyshader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert //Vertex shader entry point
            #pragma fragment frag //Fragment shader entry point
            #pragma multi_compile_instancing    //Enabling sahder variants
            #pragma instancing_options procedural:setup //Enables procedural instancing and specifies the per intance setup method
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"


            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)


            //Buffres passed from controller
            StructuredBuffer<float4> _PositionsMass;
            StructuredBuffer<float4> _Colors;

            //Setup is called for each instance of a mesh
            void setup()
            {
                uint id = unity_InstanceID; //Current body id
                float3 pos = _PositionsMass[id].xyz;

                //Transform matrix for given body
                unity_ObjectToWorld = float4x4(
                    1,0,0,pos.x,
                    0,1,0,pos.y,
                    0,0,1,pos.z,
                    0,0,0,1
                );
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //struct fro passing data from vertex to fragment shader
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            //Vertex shader, applied to every vertex of a mesh(body) instance
            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                //translation from object to clip space
                o.vertex = UnityObjectToClipPos(v.vertex);
                //Application of texture (just solid white)
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                //Coloring accordig to Colors buffer
                o.color = _Colors[unity_InstanceID];
                return o;
            }

            //Fragment shader, applied to every pixel
            fixed4 frag (v2f i) : SV_Target
            {   
                //Application of main texture at i.uv, since maintex is white, multplication with i.color makes it colored
                fixed4 col = tex2D(_MainTex, i.uv) *i.color;
                return col;
            }

            ENDCG
        }
    }
}

