float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	int4 r2;
	r0.x = -(k.x) + k.y;
	r0.x = 1 / r0.x;
	r1 = asint(texcoord + -(k.x));
	r0 = saturate(r0.x * asfloat(r1));
	r1 = asint(r0 * float4(-2, -2, -2, -2) + float4(3, 3, 3, 3));
	r0 = r0 * r0;
	r2 = asint(max(texcoord, -(k.z)));
	r2 = asint(min(asfloat(r2), k.z));
	r0 = asfloat(r1) * r0 + asfloat(r2);
	r1 = asint(texcoord / k.w);
	r2 = (asfloat(r1) >= -(asfloat(r1))) ? -1 : 0;
	r1 = asint(frac(abs(asfloat(r1))));
	r1 = (r2 != 0) ? r1 : asint(-(asfloat(r1)));
	r0 = asfloat(r1) * k.w + r0;
	r1 = (float4(0, 0, 0, 0) < texcoord) ? -1 : 0;
	r2 = (texcoord < float4(0, 0, 0, 0)) ? -1 : 0;
	r1 = -(r1) + r2;
	r1 = asint((float4)r1);
	r2.xy = asint(texcoord.xy + -(k.xy));
	r2.x = asint(dot(asfloat(r2.xy), asfloat(r2.xy)));
	r2.x = asint(sqrt(asfloat(r2.x)));
	o = asfloat(r1) * asfloat(r2.x) + r0;

	return o;
}
