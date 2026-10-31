float4 k; float3 n; float4 main(float4 a : TEXCOORD0) : COLOR { return float4(dot(a, k), dot(a.xyz, n), length(a.xy), distance(a.xyz, n)) + normalize(a); }
