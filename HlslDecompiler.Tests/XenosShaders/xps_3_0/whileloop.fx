float4 k; float4 main(float4 a : TEXCOORD0) : COLOR { float4 r = a; int i = 0; [loop] while (r.x < k.x && i < 16) { r = r * 1.5 + 0.1; i++; } return r; }
