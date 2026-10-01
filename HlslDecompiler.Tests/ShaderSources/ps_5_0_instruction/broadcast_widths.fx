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

	float3 r0;
	r0.x = dot(texcoord.xy, texcoord.xy);
	o.sv_target.xy = sqrt(r0.xx);
	o.sv_target.zw = texcoord.xx * float2(3, 3);
	r0.x = tex.CalculateLevelOfDetail(samp, texcoord.xy);
	r0.yz = GetRenderTargetSampleCount();
	o.sv_target1 = r0.xxyz;

	return o;
}
