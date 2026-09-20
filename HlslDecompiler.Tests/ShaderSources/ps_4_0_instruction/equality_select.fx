float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_TARGET
{
	float4 o;

	int3 r0;
	float4 r1;
	float4 r2;
	r0.x = (texcoord.y != k.z) ? -1 : 0;
	r1 = texcoord * k.y;
	r0.yz = (texcoord.xz == k.xw) ? -1 : 0;
	r1 = (r0.y != 0) ? r1 : texcoord;
	r0.y = asint((r0.z != 0) ? k.x : k.y);
	r2 = r1 + k.w;
	r1 = (r0.x != 0) ? r2 : r1;
	o = asfloat(r0.y) * r1;

	return o;
}
