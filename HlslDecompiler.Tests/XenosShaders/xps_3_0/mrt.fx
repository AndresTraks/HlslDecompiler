struct O { float4 a : COLOR0; float4 b : COLOR1; float d : DEPTH; }; O main(float4 a : TEXCOORD0) { O o; o.a = a; o.b = a.wzyx; o.d = a.x; return o; }
