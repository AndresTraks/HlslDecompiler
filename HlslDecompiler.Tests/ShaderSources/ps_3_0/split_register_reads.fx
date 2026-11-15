sampler2D s0;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float t0 = tex2D(s0, i.texcoord).x;
	return float4(t0 * i.texcoord, t0 * i.texcoord1);
}
