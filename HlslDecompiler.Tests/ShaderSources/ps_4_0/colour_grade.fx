float4 grade;

SamplerState samp;
SamplerState lookupSampler;
Texture2D source;
Texture3D lookup;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float3 t0 = source.Sample(samp, texcoord).xyz;
	float3 t1 = exp2(log2(max(t0, 0.0000999999975)) * grade.x + grade.y);
	float t2 = 0.212500006 * t1.x + 0.715399981 * t1.y + 0.0720999986 * t1.z;
	float3 t3 = lerp(t2, t1, grade.z);
	float3 t4 = lookup.SampleLevel(lookupSampler, 0.9375 * t3 + 0.03125, 0).xyz;
	return float4(lerp(t3, t4, grade.w), 1);
}
