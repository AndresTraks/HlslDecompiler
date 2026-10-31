float4 k[32]; float4 main(float4 p : POSITION, float4 i : TEXCOORD0) : POSITION { return p * k[(int)i.x] + k[(int)i.y]; }
