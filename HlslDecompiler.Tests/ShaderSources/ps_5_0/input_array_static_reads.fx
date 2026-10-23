struct PS_IN
{
	float4 sv_position : SV_Position;
	float4 weight[4] : WEIGHT;
	nointerpolation uint sel : SEL;
};

float4 main(PS_IN i) : SV_Target
{
	return (2 * i.weight[1] + i.weight[0] + 3 * i.weight[2] + 4 * i.weight[3]) * i.weight[i.sel & 3];
}
