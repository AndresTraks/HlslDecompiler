cbuffer K : register(b0)
{
	double k;
};

float4 main(noperspective float4 sv_position : SV_Position) : SV_TARGET
{
	double2 t0 = double2((double)sv_position.x, (double)sv_position.y * k);
	if (t0.x != t0.y) return float4(1, 0, 0, 1);
	if (t0.x >= t0.y) return float4(0, 1, 0, 1);
	return float4((float)((double)(uint)t0.y + (t0.x < t0.y ? t0.x + 1 : t0.y * 2)), 0, 0, 1);
}
