sampler2D s; float4 main(float4 p : POSITION, float2 t : TEXCOORD0) : POSITION { return p + tex2Dlod(s, float4(t, 0, 0)); }
