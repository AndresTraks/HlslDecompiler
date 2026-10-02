[earlydepthstencil]
float4 main(uint sv_innercoverage : SV_InnerCoverage) : SV_Target
{
	float4 o;

	o = (float4)(uint4)sv_innercoverage.x;

	return o;
}
