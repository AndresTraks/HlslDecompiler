float2 constant2;
float3 constant3;
float4 constant4;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord1 : TEXCOORD;
	float4 texcoord2 : TEXCOORD2;
};

float4 main(VS_IN i) : POSITION
{
	return float4(dot(constant4, i.position), dot(constant3, i.texcoord1.xyz), dot(constant2, i.texcoord2), 4);
}
