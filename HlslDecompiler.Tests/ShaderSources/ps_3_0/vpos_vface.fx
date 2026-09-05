float4 a;
float4 b;

struct PS_IN
{
	float2 vpos : VPOS;
	float vface : VFACE;
};

float4 main(PS_IN i) : COLOR
{
	return -(i.vface >= 0 ? 1 : -1) >= 0 ? b * i.vpos.yyyy : a * i.vpos.xxxx;
}
