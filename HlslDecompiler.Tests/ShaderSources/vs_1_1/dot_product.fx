float2 constant2;
float3 constant3;
float4 constant4;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

float4 main(VS_IN i) : POSITION
{
	return float4(dot(constant4, i.position), dot(constant3, i.texcoord.xyz), dot(constant2, i.texcoord1.xy), 4);
}
