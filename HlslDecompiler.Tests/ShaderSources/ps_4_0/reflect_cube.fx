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
	float t3 = length(i.texcoord - eye);
	float3 t1 = (i.texcoord - eye) / t3;
	float t2 = 2 * dot(t1, normalize(i.normal));
	float3 t0 = normalize(i.normal) * -t2 + t1;
	return env.Sample(samp, t0) * reflectivity;
}
