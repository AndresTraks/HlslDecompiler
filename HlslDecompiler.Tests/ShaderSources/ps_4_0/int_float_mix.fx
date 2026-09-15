struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation int4 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	int t0 = (uint)abs(i.texcoord1.x) >> 1;
	return float4((float)((int)(3.5 * i.texcoord.x) + i.texcoord1.y), (float)((i.texcoord1.x ^ 2) & -2147483648 ? -t0 : t0), frac(i.texcoord.x) * (float)i.texcoord1.z + (float)((i.texcoord1.y < 0 ? -i.texcoord1.y : i.texcoord1.y) + (i.texcoord1.x * 2)), (float)(i.texcoord1.w % 3) - (float)((uint)i.texcoord.x >> 1) + (i.texcoord1.y != i.texcoord1.x ? i.texcoord.x : i.texcoord.y));
}
