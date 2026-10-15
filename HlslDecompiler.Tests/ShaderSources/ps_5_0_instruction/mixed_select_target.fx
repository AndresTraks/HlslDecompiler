struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint sv_isfrontface : SV_IsFrontFace;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

uint2 main(PS_IN i) : SV_Target
{
	uint2 o;

	float4 r0;
	r0.w = (uint)asint(i.texcoord.y) >> 1;
	r0.x = i.texcoord.x;
	r0.yz = (float2)i.sv_sampleindex;
	o = (i.sv_isfrontface != 0) ? (int2)r0.xy : (int2)r0.zw;

	return o;
}
