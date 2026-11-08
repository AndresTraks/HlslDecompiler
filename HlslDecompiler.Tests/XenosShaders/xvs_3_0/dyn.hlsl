float4 k[32];

struct VS_IN
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
};

float4 main(VS_IN i) : POSITION
{
	float2 t0 = trunc(i.texcoord);
	return i.position * k[t0.x] + k[t0.y];
}
