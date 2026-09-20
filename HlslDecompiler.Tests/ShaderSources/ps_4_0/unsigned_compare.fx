int4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return (uint4)-1640531535 * k.x + 7 < 1073741824 ? texcoord : 2 * texcoord;
}
