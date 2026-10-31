int n; int m; float4 k; float4 main(float4 a : TEXCOORD0) : COLOR { float4 r = 0; for (int i = 0; i < n; i++) for (int j = 0; j < m; j++) r += a * k * (i + j); return r; }
