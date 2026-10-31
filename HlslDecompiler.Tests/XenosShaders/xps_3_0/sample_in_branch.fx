sampler2D s; float4 k; float4 main(float2 a : TEXCOORD0) : COLOR { float4 r = k; [branch] if (a.x > 0.5) r = tex2D(s, a) * k; return r; }
