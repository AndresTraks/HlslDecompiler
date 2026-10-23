struct PS_IN
{
	float4 sv_position : SV_Position;
	float4 weight[4] : WEIGHT;
	nointerpolation uint sel : SEL;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	int r1;
	r0 = i.weight[1] * float4(2, 2, 2, 2) + i.weight[0];
	r0 = i.weight[2] * float4(3, 3, 3, 3) + r0;
	r0 = i.weight[3] * float4(4, 4, 4, 4) + r0;
	r1 = i.sel & 3;
	o = r0 * i.weight[r1.x];

	return o;
}
