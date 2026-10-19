struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint sv_isfrontface : SV_IsFrontFace;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

uint2 main(PS_IN i) : SV_Target
{
	uint2 o;

	int4 r0;
	r0.w = (uint)asint(i.texcoord.y) >> 1;
	r0.x = asint(i.texcoord.x);
	r0.yz = i.sv_sampleindex;
	o = (i.sv_isfrontface != 0) ? r0.xy : r0.zw;

	return o;
}
