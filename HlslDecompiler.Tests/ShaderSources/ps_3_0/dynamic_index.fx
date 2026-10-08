int address;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return texcoord[address];
}
