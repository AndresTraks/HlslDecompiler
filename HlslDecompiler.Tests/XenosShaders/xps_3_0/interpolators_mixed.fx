float4 main(float4 c : COLOR0, float2 a : TEXCOORD1, float3 b : TEXCOORD3) : COLOR { return c * a.x + float4(b, a.y); }
