sampler2D s; float k; float4 main(float2 a : TEXCOORD0) : COLOR { return float4(0, 0, 0, tex2D(s, a).w * k); }
