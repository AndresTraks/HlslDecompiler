float4 main(float4 p : POSITION) : POSITION { return float4(frac(p.xy), sin(p.z), exp(p.w)); }
