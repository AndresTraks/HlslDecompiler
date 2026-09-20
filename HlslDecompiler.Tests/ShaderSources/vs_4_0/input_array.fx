int n;

float4 main(float4 texcoord[4] : TEXCOORD) : SV_Position
{
	return texcoord[n];
}
