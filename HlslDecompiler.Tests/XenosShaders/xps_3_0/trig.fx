float4 main(float4 a : TEXCOORD0) : COLOR { return float4(sin(a.x), cos(a.y), tan(a.z), atan2(a.w, a.x)); }
