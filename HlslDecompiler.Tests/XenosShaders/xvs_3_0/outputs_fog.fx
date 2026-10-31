float4x4 m : register(c0);
struct O { float4 p : POSITION; float4 c : COLOR1; float3 t : TEXCOORD2; float f : FOG; };
O main(float4 p : POSITION, float3 n : NORMAL, float4 c : COLOR0, float2 t : TEXCOORD3) { O o; o.p = mul(p, m); o.c = c; o.t = n * t.x; o.f = t.y; return o; }
