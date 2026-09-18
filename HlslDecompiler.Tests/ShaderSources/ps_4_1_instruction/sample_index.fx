float4 resolve;

Texture2DMS<float4, 4> source;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	float4 r1;
	r0.x = i.sv_sampleindex.x + 1;
	r0.x = r0.x & 3;
	r1.xy = (int2)i.sv_position.xy;
	r1.zw = int2(0, 0);
	r0 = asint(source.Load(r1.xy, r0.x));
	r1 = source.Load(r1.xy, i.sv_sampleindex.x);
	r0 = asint(asfloat(r0) + -(r1));
	r0 = asint(resolve.x * asfloat(r0) + r1);
	o = asfloat(r0) * resolve.y;

	return o;
}
