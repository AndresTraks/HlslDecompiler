struct PS_IN
{
	sample float4 texcoord : TEXCOORD;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_TARGET
{
	return EvaluateAttributeAtSample(i.texcoord, i.sv_sampleindex) + EvaluateAttributeSnapped(i.texcoord, int2(1, -1));
}
