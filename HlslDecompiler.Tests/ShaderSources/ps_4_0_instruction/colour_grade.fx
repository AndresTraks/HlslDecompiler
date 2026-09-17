float4 grade;

SamplerState samp;
SamplerState lookupSampler;
Texture2D source;
Texture3D lookup;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = source.Sample(samp, texcoord.xy);
	r0.xyz = max(r0.xyz, float3(0.0000999999975, 0.0000999999975, 0.0000999999975));
	r0.xyz = log2(r0.xyz);
	r0.xyz = r0.xyz * grade.xxx + grade.yyy;
	r0.xyz = exp2(r0.xyz);
	r0.w = dot(r0.xyz, float3(0.212500006, 0.715399981, 0.0720999986));
	r0.xyz = -(r0.www) + r0.xyz;
	r0.xyz = grade.zzz * r0.xyz + r0.www;
	r1.xyz = r0.xyz * float3(0.9375, 0.9375, 0.9375) + float3(0.03125, 0.03125, 0.03125);
	r1 = lookup.SampleLevel(lookupSampler, r1.xyz, 0);
	r1.xyz = -(r0.xyz) + r1.xyz;
	o.xyz = grade.www * r1.xyz + r0.xyz;
	o.w = 1;

	return o;
}
