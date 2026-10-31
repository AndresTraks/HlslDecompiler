sampler2D s0; sampler2D s1; sampler2D s2; float4 main(float2 a : TEXCOORD0) : COLOR { return tex2D(s0, a) * tex2D(s1, a.yx) + tex2D(s2, tex2D(s0, a).xy); }
