float4 a;
float4 b;
float4 c;
float4 d;
float t;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	o = a;
	o = b;
	o = c;
	o = d;

	return o;
}
