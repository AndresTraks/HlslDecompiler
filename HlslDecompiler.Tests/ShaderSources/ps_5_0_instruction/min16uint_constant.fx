min16uint4 k;

float4 main(nointerpolation min16uint4 v : V) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	r0 = v & k;
	r1 = (uint4)v >> uint4(2, 2, 2, 2);
	r0 = r0 | r1;
	o = (float4)(uint4)r0;

	return o;
}
