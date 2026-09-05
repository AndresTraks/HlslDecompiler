float k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return texcoord.x < k ? texcoord : -texcoord;
}
