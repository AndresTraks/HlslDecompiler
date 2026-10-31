float4 k; float4 main(float4 a : TEXCOORD0, float f : VFACE) : COLOR { return f > 0 ? a * k : a.wzyx; }
