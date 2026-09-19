float4 grade;

SamplerState samp;
SamplerState lookupSampler;
Texture2D source;
Texture3D lookup;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float3 t0 = exp2(log2(max(source.Sample(samp, texcoord).xyz, 0.0000999999975)) * grade.x + grade.y);
	float t1 = 0.212500006 * t0.x + 0.715399981 * t0.y + 0.0720999986 * t0.z;
	float3 t2 = lerp(t1, t0, grade.z);
	return float4(lerp(t2, lookup.SampleLevel(lookupSampler, 0.9375 * t2 + 0.03125, 0).xyz, grade.w), 1);
}
