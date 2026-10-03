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

float4 main() : SV_Target
{
	float4 t0 = probes.Sample(linearSampler, float4(direction, 2));
	return t0 + entries[entry];
}
