RWTexture2D<uint> rw : register(u1);

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	int2 t0 = (uint2)sv_position.xy;
	uint t1 = rw[t0].x;
	InterlockedMax(rw[t0.yx], 5);
	return float4(asfloat(t1), (float)t1, 0, 1);
}
