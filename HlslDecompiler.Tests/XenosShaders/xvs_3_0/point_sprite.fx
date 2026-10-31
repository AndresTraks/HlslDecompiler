struct O { float4 p : POSITION; float s : PSIZE; float4 c : COLOR0; }; float scale; O main(float4 p : POSITION, float4 c : COLOR0) { O o; o.p = p; o.s = scale / p.w; o.c = c; return o; }
