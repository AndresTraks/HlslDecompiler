float k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	if (k < texcoord.x) {
		return 0;
	}
	return texcoord;
}
