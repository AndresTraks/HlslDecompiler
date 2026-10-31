float4 main(float4 p : POSITION) : POSITION { return p * float4(0.5, 1.5, 2.5, 3.5) + float4(0.25, -1, 7, 9) * p.w + sin(p.x * 1.75); }
