cbuffer Lookup : register(b0)
{
	uint entry;
	float3 direction;
};

tbuffer Palette
{
	float4 entries[8];
};

SamplerState linearSampler;
TextureCubeArray probes : register(t1);

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0.xyz = direction.xyz;
	r0.w = 2;
	r0 = probes.Sample(linearSampler, r0);
	r1 = entries[entry];
	o = r0 + r1;

	return o;
}
