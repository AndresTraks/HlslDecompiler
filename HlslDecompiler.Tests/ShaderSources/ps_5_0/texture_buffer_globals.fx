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
	return probes.Sample(linearSampler, float4(direction, 2)) + entries[entry];
}
