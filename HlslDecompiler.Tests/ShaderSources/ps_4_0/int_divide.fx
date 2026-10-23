int a;
int b;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return (float)(a % b) + (float)(a / b);
}
