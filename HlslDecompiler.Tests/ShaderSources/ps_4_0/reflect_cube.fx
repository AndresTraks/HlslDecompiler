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
	return env.Sample(samp, reflect(normalize(i.texcoord - eye), normalize(i.normal))) * reflectivity;
}
