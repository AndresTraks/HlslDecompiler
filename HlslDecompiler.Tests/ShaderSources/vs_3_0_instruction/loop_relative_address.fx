int count;
float4 floats[8];

float4 main() : POSITION
{
	float4 o;

	float4 r0;
	r0 = 0;
	for (int i0 = 0; i0 < count; i0++) {
		r0 = r0 + floats[i0];
	}
	o = r0;

	return o;
}
