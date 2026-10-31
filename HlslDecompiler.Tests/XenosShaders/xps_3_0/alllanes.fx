float4 main(float4 a : TEXCOORD0) : COLOR { return all(a) ? 1 : (any(a) ? 0.5 : 0); }
