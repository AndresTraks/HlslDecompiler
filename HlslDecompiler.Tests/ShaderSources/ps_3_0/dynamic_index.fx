int address;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return texcoord.x * (address == 0 ? 1.0 : 0.0) + texcoord.y * (address - 1 == 0 ? 1.0 : 0.0);
}
