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
	float3 t0 = normalize(i.texcoord - eye);
	float3 t1 = normalize(i.normal);
	return env.Sample(samp, reflect(t0, t1)) * reflectivity;
}
