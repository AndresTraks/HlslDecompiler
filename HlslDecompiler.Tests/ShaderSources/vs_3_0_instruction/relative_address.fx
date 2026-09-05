int address : register(c8);
float4 floats[8];

float4 main() : POSITION
{
	float4 o;

	int a0;
	a0 = address.x;
	o = floats[a0];

	return o;
}
