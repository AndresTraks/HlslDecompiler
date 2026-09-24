cbuffer cb : register(b0)
{
	float4x4 m[8];
	int idx;
};

float4 main(float4 position : POSITION) : SV_Position
{
	return mul(mul(position, m[idx]), m[((1 + idx) & 7)]);
}
