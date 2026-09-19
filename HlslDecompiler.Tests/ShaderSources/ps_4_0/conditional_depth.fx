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

	float4 t0 = tex.Sample(samp, texcoord);
	if (t0.w < threshold) {
		discard;
	}
	o.sv_depth = t0.x > 0.5 ? nearDepth : lerp(nearDepth, farDepth, 2 * t0.x);
	o.sv_target = t0;

	return o;
}
