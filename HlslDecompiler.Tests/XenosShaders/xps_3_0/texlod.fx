sampler2D s; float4 main(float4 a : TEXCOORD0) : COLOR { return tex2Dlod(s, a) + tex2Dbias(s, a) + tex2Dproj(s, a); }
