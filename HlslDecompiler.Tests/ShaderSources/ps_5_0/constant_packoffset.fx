cbuffer B : register(b0)
{
	float2 b1 : packoffset(c0.z);
	float4 b0 : packoffset(c2);
};

float4 main() : SV_Target
{
	return float4(b1 + b0.xy, b0.zw);
}
