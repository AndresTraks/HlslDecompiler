float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return all(texcoord) ? 1 : (any(texcoord) ? 0.5 : 0);
}
