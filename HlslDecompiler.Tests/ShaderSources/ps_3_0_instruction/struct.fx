struct struct1
{
	float a;
	int b;
};

struct1 s1;
struct1 s2;

float4 main() : COLOR
{
	float4 o;

	float r0;
	r0 = s1.a;
	o = r0.x + s2.a;

	return o;
}
