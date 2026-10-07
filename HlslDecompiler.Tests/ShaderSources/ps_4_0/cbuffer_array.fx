cbuffer cb : register(b0)
{
	float4 arr[4];
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return arr[0] + arr[2];
}
