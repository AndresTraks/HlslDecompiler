struct struct1
{
	float a;
	int b;
};

struct1 s1;
struct1 s2;

float4 main() : COLOR
{
	return s1.a + s2.a;
}
