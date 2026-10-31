bool b; float4 k; float4 main(float4 a : TEXCOORD0) : COLOR { float4 r = a; if (b) r *= k; return r; }
