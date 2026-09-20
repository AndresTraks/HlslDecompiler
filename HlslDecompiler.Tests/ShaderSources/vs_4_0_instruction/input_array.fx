int n;

float4 main(float4 texcoord[4] : TEXCOORD) : SV_Position
{
	float4 o;

	float r0;
	r0 = n;
	o = texcoord[r0.x];

	return o;
}
