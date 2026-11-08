struct PS_IN
{
	float2 vpos : VPOS;
	float vface : VFACE;
};

float4 main(PS_IN i) : COLOR
{
	return float4(i.vpos, i.vface, 1);
}
