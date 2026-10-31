float4 main(float4 a : TEXCOORD0) : COLOR { float4 ip; float4 f = modf(a, ip); return f + ip + fmod(a, 0.7); }
