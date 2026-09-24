float4 v;

float4 main() : SV_Target
{
	return float4(sin(3 * v.x), cos(3 * v.x), 0, 0);
}
