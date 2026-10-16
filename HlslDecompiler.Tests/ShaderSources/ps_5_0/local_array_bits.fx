struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 x0[4];
	int4 x1[4];

	x0[0].x = i.texcoord.x;
	x0[1].x = i.texcoord.y;
	x0[2].x = i.texcoord.z;
	x0[3].x = i.texcoord.w;
	x1[0].x = i.idx;
	x1[1].x = i.idx * 2;
	x1[2].x = 3 * i.idx;
	x1[3].x = i.idx ^ 7;
	int t0 = i.idx & 3;
	float t1 = x0[t0].x + 1;
	x0[t0].x = t1;
	int t2 = i.idx + 2 & 3;
	x1[(i.idx >> 2) & 3].x = asint(x0[i.idx + 1 & 3].x);
	return float4(t1, (float)(uint)(x1[t0].x), asfloat(x1[t2].x), x0[0].x);
}
