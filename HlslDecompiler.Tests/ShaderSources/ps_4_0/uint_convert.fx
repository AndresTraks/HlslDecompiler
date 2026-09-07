float4 v;

float4 main() : SV_Target
{
	return float4(v.y + v.x, v.x * 2, v.x, trunc(v.y));
}
