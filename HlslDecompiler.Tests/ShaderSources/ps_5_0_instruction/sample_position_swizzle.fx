Texture2DMS<float4, 4> ms;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	int2 r1;
	r0.xy = (int2)i.sv_position.xy;
	r0.zw = int2(0, 0);
	r0 = asint(ms.Load(r0.xy, i.sv_sampleindex));
	uint3 dimensions0;
	ms.GetDimensions(dimensions0.x, dimensions0.y, dimensions0.z);
	r1.x = dimensions0.x;
	r1.x = r1.x << 2;
	r1.x = asint((float)(uint)r1.x);
	r1 = asint(ms.GetSamplePosition(i.sv_sampleindex).yx);
	o = asfloat(r0) * asfloat(r1.y) + asfloat(r1.x);

	return o;
}
