Buffer<float4> colours;
Buffer<uint> indices;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	r0.x = asint(texcoord.x * 16);
	r0.x = (int)asfloat(r0.x);
	r0 = indices.Load(r0.x);
	r0 = asint(colours.Load(r0.x));
	o = asfloat(r0) * texcoord.y;

	return o;
}
