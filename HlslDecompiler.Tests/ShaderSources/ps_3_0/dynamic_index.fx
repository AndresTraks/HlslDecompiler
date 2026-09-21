int address;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float2 t0 = float2(address == 0 ? 1.0 : 0.0, address - 1 == 0 ? 1.0 : 0.0);
	return texcoord.x * t0.x + texcoord.y * t0.y;
}
