cbuffer cb : register(b0)
{
	int a;
	uint b;
	int4 v;
};

float4 main() : SV_Target
{
	return float4((float)((a * 8) | (a >> 2)), (float)((b >> 1) & 15), (float2)(v.xw % 7 + v.xw / 3));
}
