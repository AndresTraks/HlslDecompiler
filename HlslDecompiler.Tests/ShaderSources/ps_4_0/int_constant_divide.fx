int value;
uint uvalue;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return float4((float)(value / 7), (float)(value % 13), (float)(uvalue / 10), (float)(uvalue % 6)) * texcoord;
}
