RWStructuredBuffer<uint> b : register(u1);
RWTexture2D<float> rt : register(u2);

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	float r1;
	r0.x = b[i.idx];
	o.x = asfloat(r0.x);
	b[i.idx] = 5;
	r0 = (uint4)i.sv_position.xyyy;
	r1 = rt[r0.xw].x;
	o.y = r1.x;
	rt[r0.xy] = float4(2, 2, 2, 2);
	r0.y = 0;
	r0.x = i.idx + 1;
	InterlockedAdd(b[r0.x], 3, r1);
	r0.x = b[r0.x];
	o.w = (float)(uint)r0.x;
	o.z = (float)(uint)r1.x;

	return o;
}
