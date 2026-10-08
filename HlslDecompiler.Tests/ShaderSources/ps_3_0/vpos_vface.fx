float4 a;
float4 b;

struct PS_IN
{
	float2 vpos : VPOS;
	float vface : VFACE;
};

float4 main(PS_IN i) : COLOR
{
	return i.vface >= 0 ? a * i.vpos.xxxx : b * i.vpos.yyyy;
}
