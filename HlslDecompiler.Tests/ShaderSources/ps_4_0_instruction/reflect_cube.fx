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
	float4 o;

	float4 r0;
	float3 r1;
	r0.xyz = i.texcoord.xyz + -(eye.xyz);
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = 1 / sqrt(r0.w);
	r0.xyz = r0.www * r0.xyz;
	r0.w = dot(i.normal.xyz, i.normal.xyz);
	r0.w = 1 / sqrt(r0.w);
	r1 = r0.www * i.normal.xyz;
	r0.w = dot(r0.xyz, r1.xyz);
	r0.w = r0.w + r0.w;
	r0.xyz = r1.xyz * -(r0.www) + r0.xyz;
	r0 = env.Sample(samp, r0.xyz);
	o = r0 * reflectivity;

	return o;
}
