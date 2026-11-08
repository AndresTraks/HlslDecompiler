float3 l;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = normalize(normalize(i.texcoord1) + l);
	return pow(saturate(dot(normalize(i.texcoord), t0)), 16);
}
