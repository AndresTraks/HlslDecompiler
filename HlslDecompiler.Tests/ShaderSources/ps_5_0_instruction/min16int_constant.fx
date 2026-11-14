min16int4 k;

float4 main(nointerpolation min16int4 v : V) : SV_Target
{
	float4 o;

	int4 r0;
	r0 = v * k + int4(3, 3, 3, 3);
	r0 = r0 >> int4(1, 1, 1, 1);
	o = (float4)r0;

	return o;
}
