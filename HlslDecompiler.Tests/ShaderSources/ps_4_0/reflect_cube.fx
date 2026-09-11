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
	float t5 = length(i.texcoord - eye);
	float t2 = (i.texcoord.x - eye.x) / t5;
	float t3 = (i.texcoord.y - eye.y) / t5;
	float t4 = 2 * dot(float3(t2, t3, (i.texcoord.z - eye.z) / t5), normalize(i.normal));
	float t0 = i.normal.x / length(i.normal) * -t4 + t2;
	float t1 = i.normal.y / length(i.normal) * -t4 + t3;
	float t6 = i.normal.z / length(i.normal) * -t4 + (i.texcoord.z - eye.z) / t5;
	return env.Sample(samp, float3(t0, t1, t6)) * reflectivity;
}
