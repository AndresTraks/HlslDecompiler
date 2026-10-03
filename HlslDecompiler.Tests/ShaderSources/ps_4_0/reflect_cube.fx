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
	float3 t0 = reflect(normalize(i.texcoord - eye), normalize(i.normal));
	float4 t1 = env.Sample(samp, t0);
	return t1 * reflectivity;
}
