struct PS_IN
{
	nointerpolation int4 a : A;
	float4 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	bool t0 = i.texcoord.z < 0 && i.texcoord.y < i.texcoord.x || ((i.a.w | i.a.y) | (i.a.x | i.a.z)) != 0;
	bool t1 = any((i.a.xzyw > 1) != (i.texcoord.xzyw > 0));
	bool t2 = any(i.a.zw <= 1) && any(i.a.xy > 1);
	return float4(t2, t0, all(i.a == i.a.wxyz), t1);
}
