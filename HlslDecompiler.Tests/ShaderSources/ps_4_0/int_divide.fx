int a;
int b;

float4 main() : SV_Target
{
	return (float4)(a & -2147483648 ? -abs(a) % abs(b) : abs(a) % abs(b)) + (float4)((b ^ a) & -2147483648 ? -abs(a) / abs(b) : abs(a) / abs(b));
}
