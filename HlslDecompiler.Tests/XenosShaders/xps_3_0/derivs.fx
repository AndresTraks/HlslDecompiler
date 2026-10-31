float4 main(float2 a : TEXCOORD0) : COLOR { return float4(ddx(a), ddy(a)); }
