struct PS_IN
{
	float2 vpos : VPOS;
	float vface : VFACE;
};

float4 main(PS_IN i) : COLOR
{
	float t0 = i.vface >= 0 ? i.vpos.x : -i.vpos.x;
	return float4(abs(t0), abs(i.vpos.y), 2 * ((1 / t0) >= 0 ? 1 : 0) - 1, 1);
}
