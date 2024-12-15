float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 r0 = -1;
	clip(r0.x);
	return texcoord;
}
