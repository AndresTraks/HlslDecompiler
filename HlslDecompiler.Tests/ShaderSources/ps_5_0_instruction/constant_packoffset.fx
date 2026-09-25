cbuffer B : register(b0)
{
	float2 b1 : packoffset(c0.z);
	float4 b0 : packoffset(c2);
};

float4 main() : SV_Target
{
	float4 o;

	o.xy = b1.xy + b0.xy;
	o.zw = b0.zw;

	return o;
}
