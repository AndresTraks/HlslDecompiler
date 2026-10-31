float4 main(float4 a : TEXCOORD0, float4 b : TEXCOORD1) : COLOR { return a > b ? a : b; }
