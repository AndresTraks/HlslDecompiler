struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	float4 x0[4];
	int4 x1[4];
	x0[0].x = i.texcoord.x;
	x0[1].x = i.texcoord.y;
	x0[2].x = i.texcoord.z;
	x0[3].x = i.texcoord.w;
	x1[0].x = i.idx;
	r0.x = i.idx << 1;
	x1[1].x = r0.x;
	r0.x = i.idx * 3;
	x1[2].x = r0.x;
	r0.x = i.idx ^ 7;
	x1[3].x = r0.x;
	r0.x = i.idx & 3;
	r0.y = asint(x0[r0.x].x);
	r0.y = asint(asfloat(r0.y) + 1);
	x0[r0.x].x = asfloat(r0.y);
	o.x = asfloat(r0.y);
	r0.yz = i.idx + int2(1, 2);
	r0.yz = r0.yz & int2(3, 3);
	r0.y = asint(x0[r0.y].x);
	o.w = x0[0].x;
	r0.w = ((uint)i.idx >> 2) & 3;
	x1[r0.w].x = r0.y;
	r0.x = x1[r0.x].x;
	o.z = asfloat(x1[r0.z].x);
	o.y = (float)(uint)r0.x;

	return o;
}
