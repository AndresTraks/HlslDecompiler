struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation int4 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float t0 = i.texcoord.y + i.texcoord.x;
	return float4(-t0 * i.texcoord.z + (all(i.texcoord.xw < i.texcoord.yz) ? i.texcoord.x : i.texcoord.y), t0 * (i.texcoord.z - i.texcoord.w) / (i.texcoord.x - i.texcoord.y) * (i.texcoord.x > 0 ? i.texcoord.y > 0 ? 1 : 2 : 3), (float)((~i.texcoord1.x & i.texcoord1.y) + ((i.texcoord1.w * 4) ^ ((i.texcoord1.y & i.texcoord1.x) | i.texcoord1.z))), (i.texcoord.y < i.texcoord.x ? -abs(i.texcoord.z) : saturate(i.texcoord.w)) + (float)((i.texcoord1.y + i.texcoord1.x) * i.texcoord1.z % 5));
}
