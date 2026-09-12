int a;
uint b;
int4 v;

float4 main() : SV_Target
{
	return float4((float)((a >> 2) | (a * 8)), (float)(((uint)b >> 1) & 15), (float2)(v.xw / 3 + v.xw % 7));
}
