float4 k; float4 main(float4 a : TEXCOORD0) : COLOR { float4 r; if (a.x > k.x) r = a * 2; else if (a.y > k.y) r = a.yzwx; else r = k - a; return r; }
