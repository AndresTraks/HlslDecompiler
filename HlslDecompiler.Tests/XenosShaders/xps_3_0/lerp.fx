float4 k; float4 main(float4 a : TEXCOORD0, float4 b : TEXCOORD1) : COLOR { return lerp(a, b, k.x) + saturate(a * b) + step(a, b) + max(a, b) + min(a, b); }
