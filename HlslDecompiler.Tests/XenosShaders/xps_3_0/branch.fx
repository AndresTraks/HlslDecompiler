float4 k; float4 main(float4 a : TEXCOORD0) : COLOR { float4 r = a; [branch] if (a.x > k.x) r = sqrt(a) * k; else r = a.yzwx; return r; }
