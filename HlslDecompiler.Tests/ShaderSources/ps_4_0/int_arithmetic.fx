int n;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return (3 * n - 7) * texcoord;
}
