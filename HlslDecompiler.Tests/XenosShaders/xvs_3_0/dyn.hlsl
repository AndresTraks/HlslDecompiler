float4 k[32];

struct VS_IN
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
};

float4 main(VS_IN i) : POSITION
{
	float2 t0 = abs(i.texcoord) - frac(abs(i.texcoord));
	float2 t1 = i.texcoord >= 0 ? t0 : -t0;
	return i.position * k[t1.x] + k[t1.y];
}
