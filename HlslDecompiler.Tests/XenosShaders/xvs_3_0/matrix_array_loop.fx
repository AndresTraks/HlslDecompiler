float4x4 m[4]; int n; float4 main(float4 p : POSITION) : POSITION { float4 r = p; for (int i = 0; i < n; i++) r = mul(r, m[i]); return r; }
