float4 main(float4 a : TEXCOORD0) : COLOR { return float4(pow(a.x, a.y), log2(a.z), exp2(a.w), rsqrt(a.x)) + frac(a) + floor(a) + sign(a) + abs(a); }
