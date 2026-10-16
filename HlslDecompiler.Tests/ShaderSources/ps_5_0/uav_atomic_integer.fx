Texture2DArray<uint4> ta;
Texture2DMS<int2, 4> ms;
RWTexture2D<uint> rw : register(u1);

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	int2 t0 = (uint2)i.sv_position.xy;
	int t1 = rw[t0].x;
	int2 t2 = ms.Load((int2)i.sv_position.xy, i.idx & 3).xy;
	int2 t3 = ta.Load(int4((int2)i.sv_position.xy, i.idx & 3, 0), int2(1, -1)).yw;
	InterlockedMax(rw[t0.yx], ta.Load(int4((int2)i.sv_position.xy, i.idx & 3, 0), int2(1, -1)).x);
	return float4(asfloat(t3.x), (float)t2.x, asfloat(t1 ^ t2.y), (float)t3.y);
}
