float3 eye;
float reflectivity;

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
	float t1 = 2 * dot(t0, normalize(i.normal));
	return env.Sample(samp, normalize(i.normal) * -t1 + t0) * reflectivity;
}
