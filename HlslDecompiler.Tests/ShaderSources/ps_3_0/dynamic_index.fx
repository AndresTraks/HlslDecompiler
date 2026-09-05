int address;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return texcoord.x * (-abs(address) >= 0 ? 1 : 0) + texcoord.y * (-abs(address - 1) >= 0 ? 1 : 0);
}
