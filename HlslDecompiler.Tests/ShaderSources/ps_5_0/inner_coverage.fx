[earlydepthstencil]
float4 main(uint sv_innercoverage : SV_InnerCoverage) : SV_Target
{
	return (float4)sv_innercoverage;
}
