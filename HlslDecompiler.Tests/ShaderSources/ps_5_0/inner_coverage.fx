[earlydepthstencil]
float4 main(uint sv_innercoverage : SV_InnerCoverage) : SV_Target
{
	return (float)sv_innercoverage;
}
