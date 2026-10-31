sampler2D s; samplerCUBE c; float4 main(float4 a : TEXCOORD0) : COLOR { return tex2Dbias(s, float4(a.xy, 0, a.w)) + texCUBElod(c, float4(a.xyz, a.w)); }
