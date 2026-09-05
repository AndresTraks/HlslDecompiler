int mode;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	r0 = texcoord;
	r0 = texcoord + texcoord;
	r0 = float4(1, 1, 1, 1);
	o = r0;

	return o;
}
