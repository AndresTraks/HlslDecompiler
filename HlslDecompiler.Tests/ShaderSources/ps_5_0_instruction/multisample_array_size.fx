Texture2DMSArray<float4, 4> ms;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0.xy = (int2)i.sv_position.xy;
	r0.zw = int2(1, 0);
	r0 = ms.Load(r0.xyz, i.sv_sampleindex.x);
	uint4 dimensions0;
	ms.GetDimensions(dimensions0.x, dimensions0.y, dimensions0.z, dimensions0.w);
	r1.xyz = dimensions0.xyz;
	r1.xyz = (float3)(uint3)r1.xyz;
	r1.w = 4;
	o = r0 + r1;

	return o;
}
