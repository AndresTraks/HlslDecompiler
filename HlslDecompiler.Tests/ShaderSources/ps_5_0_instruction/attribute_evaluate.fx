struct PS_IN
{
	sample float4 texcoord : TEXCOORD;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_TARGET
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = EvaluateAttributeAtSample(i.texcoord, i.sv_sampleindex);
	r1 = EvaluateAttributeSnapped(i.texcoord, int2(1, -1));
	o = r0 + r1;

	return o;
}
