float4 v;

float4 main() : SV_Target
{
	return float4((float)((int)v.y + (uint)v.x), (float)((uint)v.x * 2), (float)(uint)v.x, trunc(v.y));
}
