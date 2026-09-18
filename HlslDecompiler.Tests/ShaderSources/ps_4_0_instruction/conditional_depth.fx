float threshold;
float nearDepth;
float farDepth;

SamplerState samp;
Texture2D tex;

struct PS_OUT
{
	float4 sv_target : SV_Target;
	float sv_depth : SV_Depth;
};

PS_OUT main(float2 texcoord : TEXCOORD)
{
	PS_OUT o;

	float4 r0;
	int3 r1;
	r0 = tex.Sample(samp, texcoord.xy);
	r1.x = (r0.w < threshold) ? -1 : 0;
	if (r1.x != 0) discard;
	r1.x = (0.5 < r0.x) ? -1 : 0;
	r1.y = asint(r0.x + r0.x);
	r1.z = asint(-(nearDepth) + farDepth);
	r1.y = asint(asfloat(r1.y) * asfloat(r1.z) + nearDepth);
	o.sv_depth = (r1.x != 0) ? nearDepth : asfloat(r1.y);
	o.sv_target = r0;

	return o;
}
