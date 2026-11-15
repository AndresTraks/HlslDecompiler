float4 k;
sampler2D s0;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float t0 = tex2D(s0, i.texcoord).x;
	return float4(i.texcoord1 * t0 + k.xyz, i.texcoord.x * t0 + k.w);
}
