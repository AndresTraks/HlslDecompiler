float4x4 viewProj;
float3 right;
float3 up;
float size;

struct VS_IN
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
};

float4 main(VS_IN i) : SV_Position
{
	return mul(float4((right * i.texcoord.x + i.texcoord.y * up) * size + i.position.xyz, 1), viewProj);
}
