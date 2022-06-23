float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	clip(-1);
	return texcoord;
}
