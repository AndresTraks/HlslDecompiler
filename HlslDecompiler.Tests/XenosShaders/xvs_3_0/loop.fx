int n; float4 k[8]; float4 main(float4 p : POSITION) : POSITION { float4 r = p; for (int i = 0; i < n; i++) r += k[i] * p.x; return r; }
