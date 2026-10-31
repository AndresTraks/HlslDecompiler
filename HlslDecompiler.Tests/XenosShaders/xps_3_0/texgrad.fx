sampler2D s; float4 main(float2 a : TEXCOORD0, float2 g : TEXCOORD1) : COLOR { return tex2Dgrad(s, a, g, g.yx); }
