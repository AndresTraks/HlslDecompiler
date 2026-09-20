int4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return (float4)(-1640531535 * k.x - 1640531527 & 65535) * texcoord;
}
