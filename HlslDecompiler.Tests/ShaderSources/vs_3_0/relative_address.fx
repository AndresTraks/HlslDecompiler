int address : register(c8);
float4 floats[8];

float4 main() : POSITION
{
	return floats[address];
}
