float a;
float b;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float2 r0;
	o = (r0.x != 0) ? texcoord : -(texcoord);

	return o;
}
