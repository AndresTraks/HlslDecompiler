float4x4 m; float3x3 n; float4 main(float4 a : TEXCOORD0) : COLOR { return mul(a, m) + float4(mul(a.xyz, n), 0); }
