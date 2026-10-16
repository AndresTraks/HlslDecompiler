Texture2D tx;
RWTexture2D<uint> rw : register(u1);

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	uint2 t0 = (uint2)sv_position.xy;
	float t1 = asfloat(rw[t0].x);
	InterlockedMax(rw[t0.yx], 5);
	return float4(t1, tx.Load(int3(t0.yx, 0)).xyz);
}
