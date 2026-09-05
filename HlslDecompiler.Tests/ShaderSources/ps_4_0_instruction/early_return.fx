float k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	o = float4(0, 0, 0, 0);
	o = texcoord;

	return o;
}
