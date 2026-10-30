Texture2DMS<float4> t;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	float r2;
	r0.xy = (int2)i.sv_position.xy;
	r0.zw = int2(0, 0);
	r0 = asint(t.Load(r0.xy, i.sv_sampleindex));
	uint3 dimensions0;
	t.GetDimensions(dimensions0.x, dimensions0.y, dimensions0.z);
	r1.x = dimensions0.x;
	r1.x = asint((float)(uint)r1.x);
	r1.w = asint(asfloat(r1.x) * 0.0000999999975);
	float3 dimensions1;
	t.GetDimensions(dimensions1.x, dimensions1.y, dimensions1.z);
	r1.z = asint(dimensions1.z);
	r1.xy = asint(t.GetSamplePosition(i.sv_sampleindex));
	r2 = asfloat(r1.x) + 0.5;
	o = asfloat(r0) * r2.x + asfloat(r1);

	return o;
}
