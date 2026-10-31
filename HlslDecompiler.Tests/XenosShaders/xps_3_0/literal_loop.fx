float4 a[6] : register(c0);
float4 main(float4 c : TEXCOORD0) : COLOR { float4 r = c; for (int i = 0; i < 6; i++) r = r * a[i] + float4(0.1, 0.2, 0.3, 0.4) * i + float4(1.1,1.2,1.3,1.4); return r + float4(5,6,7,8); }
