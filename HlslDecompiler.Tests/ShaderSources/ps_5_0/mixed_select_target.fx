struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint sv_isfrontface : SV_IsFrontFace;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

uint2 main(PS_IN i) : SV_Target
{
	return i.sv_isfrontface ? int2(asint(i.texcoord.x), i.sv_sampleindex) : int2(i.sv_sampleindex, (uint)asint(i.texcoord.y) >> 1);
}
