float3 l; float4 main(float3 n : TEXCOORD0, float3 v : TEXCOORD1) : COLOR { float3 h = normalize(normalize(v) + l); return pow(saturate(dot(normalize(n), h)), 16); }
