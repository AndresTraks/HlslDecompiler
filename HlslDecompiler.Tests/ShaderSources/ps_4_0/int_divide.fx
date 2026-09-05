int a;
int b;

float4 main() : SV_Target
{
	return (a & -2147483648 ? -abs(a) % abs(b) : abs(a) % abs(b)) + ((b ^ a) & -2147483648 ? -abs(a) / abs(b) : abs(a) / abs(b));
}
