int a;
int b;

float4 main() : SV_Target
{
	return (float4)(a % b) + (float4)(a / b);
}
