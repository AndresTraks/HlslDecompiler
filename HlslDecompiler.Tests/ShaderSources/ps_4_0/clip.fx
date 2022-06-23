float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	clip(-1);
	return texcoord;
}
