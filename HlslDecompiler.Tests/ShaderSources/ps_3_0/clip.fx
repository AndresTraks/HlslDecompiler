float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = -1;
	clip(t0);
	return texcoord;
}
