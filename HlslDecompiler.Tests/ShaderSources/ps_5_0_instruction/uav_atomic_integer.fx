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
	float4 o;

	int3 r0;
	int4 r1;
	int3 r2;
	r0.xy = (uint2)i.sv_position.yx;
	r0.z = rw[r0.yx].x;
	r1.z = i.idx & 3;
	r1.xy = (int2)i.sv_position.xy;
	r1.w = 0;
	r2 = ta.Load(r1, int2(1, -1)).xyw;
	r1.xy = ms.Load(r1.xy, r1.zz).xy;
	InterlockedMax(rw[r0.xy], (uint)r2.x);
	o.z = asfloat(r0.z ^ r1.y);
	o.y = (float)r1.x;
	o.w = (float)(uint)r2.z;
	o.x = asfloat(r2.y);

	return o;
}
