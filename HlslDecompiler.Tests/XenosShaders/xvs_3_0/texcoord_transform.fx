float4x4 tm; float4 main(float4 p : POSITION, float2 t : TEXCOORD0, out float2 ot : TEXCOORD0) : POSITION { ot = mul(float4(t, 0, 1), tm).xy; return p; }
