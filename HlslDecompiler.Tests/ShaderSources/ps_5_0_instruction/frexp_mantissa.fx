float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int3 r0;
	float r1;
	r0.x = asint(texcoord.w) & 2139095040;
	r0.x = r0.x + -1056964608;
	r0.y = (texcoord.w != 0) ? -1 : 0;
	r0.x = r0.y & r0.x;
	r0.x = r0.x >> 23;
	r0.x = asint((float)r0.x);
	r0.z = (1056964608 & ~(((1 << 23) - 1) << 0)) | ((asint(texcoord.w) << 0) & (((1 << 23) - 1) << 0));
	r0.y = r0.y & r0.z;
	o.w = asfloat(r0.x) + asfloat(r0.y);
	r0.x = asint(texcoord.z * 0.333333343);
	r0.y = (asfloat(r0.x) >= -(asfloat(r0.x))) ? -1 : 0;
	r0.x = asint(frac(abs(asfloat(r0.x))));
	r0.x = (r0.y != 0) ? r0.x : asint(-(asfloat(r0.x)));
	r0.y = asint(exp2(texcoord.y));
	r0.y = asint(asfloat(r0.y) * texcoord.x);
	o.z = asfloat(r0.x) * 3 + asfloat(r0.y);
	r0.x = asint(sin(texcoord.x));
	r1 = cos(texcoord.x);
	o.x = asfloat(r0.x) + r1.x;
	o.y = texcoord.y;

	return o;
}
