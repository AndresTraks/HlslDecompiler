sampler2D s; float k; float4 main(float2 a : TEXCOORD0) : COLOR { float4 t = tex2D(s, a); clip(t.w - k); return t; }
