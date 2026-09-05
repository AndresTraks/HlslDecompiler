float cutoff;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	clip(texcoord.w - cutoff);
	return texcoord;
}
