RWStructuredBuffer<uint> b : register(u1);
RWTexture2D<float> rt : register(u2);

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float t0 = asfloat(b[i.idx]);
	b[i.idx] = 5;
	int2 t1;
	t1.x = (uint)i.sv_position.x;
	uint2 t2 = (uint)i.sv_position.y;
	t1.y = (uint)i.sv_position.y;
	float t3 = rt[t1].x;
	rt[int2(t1.x, t2.x)] = 2;
	uint t4 = i.idx + 1;
	int t5;
	InterlockedAdd(b[t4], 3, t5);
	return float4(t0, t3, (float)(uint)t5, (float)(b[t4]));
}
