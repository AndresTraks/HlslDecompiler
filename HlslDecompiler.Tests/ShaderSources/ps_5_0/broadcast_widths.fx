SamplerState samp;
Texture2D tex;

struct PS_OUT
{
	float4 sv_target : SV_Target;
	float4 sv_target1 : SV_Target1;
};

PS_OUT main(float2 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.sv_target = float4(length(texcoord), length(texcoord), 3 * texcoord.xx);
	o.sv_target1 = float4(tex.CalculateLevelOfDetail(samp, texcoord), tex.CalculateLevelOfDetail(samp, texcoord), GetRenderTargetSampleCount(), GetRenderTargetSampleCount());

	return o;
}
