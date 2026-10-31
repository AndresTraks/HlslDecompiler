float3 k; float4 main(float3 a : TEXCOORD0) : COLOR { return float4(cross(a, k), 1) + float4(reflect(a, k), 0); }
