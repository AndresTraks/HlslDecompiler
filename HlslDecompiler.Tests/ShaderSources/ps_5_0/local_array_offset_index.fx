struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation int4 k : K;
};

float4 main(PS_IN i) : SV_Target
{
	float4 x0[12];

	x0[0].x = 0;
	x0[1].x = i.texcoord.x;
	float3 t0 = 2 * i.texcoord.yzw;
	x0[2].x = 2 * i.texcoord.x;
	x0[3].x = 0;
	x0[4].x = i.texcoord.y;
	x0[5].x = t0.x;
	x0[6].x = 0;
	x0[7].x = i.texcoord.z;
	x0[8].x = t0.y;
	x0[9].x = 0;
	x0[10].x = i.texcoord.w;
	x0[11].x = t0.z;
	int2 t1 = int2(i.k.w % 3, 3 * (i.k.z & 3));
	int t2 = i.k.x & 3;
	x0[3 * t2 + i.k.y % 3].x = 9;
	return float4(x0[t1.y + 1].x, x0[t1.x + 3].x, x0[11].x, x0[0].x);
}
