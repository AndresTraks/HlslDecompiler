float4 k[32];

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

float4 main(VS_IN i) : POSITION
{
	return i.position * k[(int)i.texcoord.x] + k[(int)i.texcoord.y];
}
