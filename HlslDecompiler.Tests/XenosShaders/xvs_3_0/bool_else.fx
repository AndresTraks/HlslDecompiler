bool b; float4 k; float4 main(float4 p : POSITION) : POSITION { float4 r; if (b) r = p * k; else r = p + k; return r; }
