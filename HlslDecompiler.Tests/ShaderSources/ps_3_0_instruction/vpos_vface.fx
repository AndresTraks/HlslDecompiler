float4 a;
float4 b;

struct PS_IN
{
	float2 vpos : VPOS;
	float vface : VFACE;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float r0;
	float4 r1;
	float4 r2;
	r0 = (i.vface >= 0) ? 1 : -1;
	r1 = a * i.vpos.x;
	r2 = b * i.vpos.y;
	o = (-r0.x >= 0) ? r2 : r1;

	return o;
}
