float4 k[4]; int n; bool b; float4 main(float4 a : TEXCOORD0) : COLOR { float4 r = a; if (b) { for (int i = 0; i < n; i++) { if (r.x > k[i].x) r -= k[i]; else r += k[i] * 0.5; } } return r; }
