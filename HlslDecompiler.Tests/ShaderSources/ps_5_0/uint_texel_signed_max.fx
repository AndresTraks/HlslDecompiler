cbuffer C : register(b0)
{
	int bias;
};

Texture2D<uint2> t;

int4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	int3 t0 = int3((int2)sv_position.xy, 0);
	int2 t1 = t.Load(t0).xy - bias;
	int2 t2 = t1;
	int2 t3 = (uint2)max(t1, -t2) >> 1;
	return int4((t2 ^ 2) & -2147483648 ? -t3 : t3, abs(t2.y) < abs(t2.x) ? 1 : 0, sign(t2.x - t2.y));
}
