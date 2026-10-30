cbuffer c : register(b0)
{
	float4 a;
	float4 b;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	r0 = (a < texcoord) ? -1 : 0;
	r1.xyz = (texcoord.xyz < b.xyz) ? -1 : 0;
	r1.w = -1;
	r0 = (r0 != r1) ? -1 : 0;
	r0 = r0 & int4(1065353216, 1065353216, 1065353216, 1065353216);
	o = asfloat(r0) + texcoord;

	return o;
}
