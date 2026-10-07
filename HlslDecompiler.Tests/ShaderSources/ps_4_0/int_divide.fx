int a;
int b;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return (float4)(a % b) + (float4)(a / b);
}
