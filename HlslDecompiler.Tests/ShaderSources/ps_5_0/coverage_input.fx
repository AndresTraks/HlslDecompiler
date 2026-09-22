struct PS_IN
{
	float4 color : COLOR;
	uint sv_coverage : SV_Coverage;
};

float4 main(PS_IN i) : SV_Target
{
	return 0.03125 * (float4)countbits(i.sv_coverage) * i.color;
}
