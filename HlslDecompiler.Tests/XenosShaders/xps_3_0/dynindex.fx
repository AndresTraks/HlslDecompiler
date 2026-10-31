float4 k[16]; float4 main(float4 a : TEXCOORD0) : COLOR { return k[(int)a.x] + k[(int)a.y + 3]; }
