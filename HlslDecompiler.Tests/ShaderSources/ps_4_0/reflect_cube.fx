cbuffer cb : register(b0)
{
	float3 eye;
	float reflectivity;
};

SamplerState samp;
TextureCube env;

struct PS_IN
{
	float3 normal : NORMAL;
	float3 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 t0 = env.Sample(samp, reflect(normalize(i.texcoord - eye), normalize(i.normal)));
	return t0 * reflectivity;
}
