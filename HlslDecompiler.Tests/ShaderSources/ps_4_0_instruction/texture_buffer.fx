tbuffer Params
{
	float4 tint;
	float4 offset;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tint;
	r1 = offset;
	o = texcoord * r0 + r1;

	return o;
}
