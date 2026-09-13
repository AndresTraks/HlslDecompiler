uint selector;

float4 main(nointerpolation uint4 texcoord : TEXCOORD) : SV_Target
{
	int4 x0[4];

	x0[0].x = texcoord.x;
	x0[1].x = texcoord.y * 2;
	x0[2].x = texcoord.z + 1;
	x0[3].x = texcoord.w;
	int t0 = texcoord.x + selector & 3;
	int t1 = x0[t0].x + 5;
	x0[t0].x = t1;
	return float4((float)t1, (float)(x0[3].x), (float)(x0[t0 + 1 & 3].x), (float)t0);
}
