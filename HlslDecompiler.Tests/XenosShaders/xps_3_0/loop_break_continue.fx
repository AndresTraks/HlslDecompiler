float4 k[8]; int n; float4 main(float4 a : TEXCOORD0) : COLOR { float4 r = 0; for (int i = 0; i < n; i++) { if (k[i].w < 0) continue; r += a * k[i]; if (r.x > 10) break; } return r; }
