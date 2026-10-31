float4 main(float4 p : POSITION, int4 i : BLENDINDICES) : POSITION { return p * i; }
