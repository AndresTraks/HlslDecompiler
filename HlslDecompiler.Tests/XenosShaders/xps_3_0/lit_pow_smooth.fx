float4 k; float4 main(float4 a : TEXCOORD0) : COLOR { return lit(a.x, a.y, k.x) + pow(abs(a), k.y) + smoothstep(k.z, k.w, a); }
