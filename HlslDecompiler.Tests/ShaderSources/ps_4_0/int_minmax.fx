int4 bounds;
uint scale;

struct PS_IN
{
	nointerpolation int4 texcoord : TEXCOORD;
	nointerpolation uint2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	int t0 = min(i.texcoord.x, bounds.x);
	int t1 = max(i.texcoord.y, bounds.y);
	return float4((float)(t1 + t0), (float)(abs(i.texcoord.z - bounds.z) - (~i.texcoord.w & bounds.w)), (float)(i.texcoord1.x * scale + min(i.texcoord1.y, scale)), (float)(i.texcoord.x < i.texcoord.y ? t0 : t1));
}
