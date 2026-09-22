struct PS_IN
{
	float4 color : COLOR;
	uint sv_coverage : SV_Coverage;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float r0;
	r0 = countbits(i.sv_coverage.x);
	r0 = (float)(uint)r0.x;
	r0 = r0.x * 0.03125;
	o = r0.x * i.color;

	return o;
}
