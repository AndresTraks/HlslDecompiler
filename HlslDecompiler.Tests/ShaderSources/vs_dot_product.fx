float3 constant3;
float4 constant4;

struct VS_IN
{
	float4 texcoord : POSITION;
	float3 texcoord1 : TEXCOORD;
	float2 texcoord2 : TEXCOORD2;
};

float4 main(VS_IN i) : POSITION
{
	return float4(dot(constant4, i.texcoord), dot(constant3, i.texcoord1), float2(3, 4));
}
