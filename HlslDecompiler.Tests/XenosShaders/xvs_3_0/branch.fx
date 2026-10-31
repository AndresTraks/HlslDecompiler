bool b; float4x4 m; float4 main(float4 p : POSITION, float3 n : NORMAL) : POSITION { float4 r = mul(p, m); if (b) r.xyz += n; return r; }
