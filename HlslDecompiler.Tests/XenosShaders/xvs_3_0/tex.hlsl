sampler2D s;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

float4 main(VS_IN i) : POSITION
{
	float4 t0 = tex2Dlod(s, float4(i.texcoord.xyy, 0));
	return i.position + t0;
}
