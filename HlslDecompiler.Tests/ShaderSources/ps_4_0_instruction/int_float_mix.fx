struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation int4 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	int3 r1;
	r0.x = asfloat(i.texcoord1.x << 1);
	r0.y = (i.texcoord1.y < 0) ? -1 : 0;
	r1 = -i.texcoord1.xyw;
	r0.y = (asint(r0.y) != 0) ? r1.y : i.texcoord1.y;
	r0.zw = asfloat(max(r1.xz, i.texcoord1.xw));
	r0.x = asfloat((int)r0.y + asint(r0.x));
	r0.x = (float)asint(r0.x);
	r0.y = frac(i.texcoord.x);
	r1.x = (float)(int)i.texcoord1.z;
	o.z = r0.y * r1.x + r0.x;
	r0.x = asfloat(asuint(r0.w) - asuint(r0.w) / 3 * 3);
	r0.y = (uint)asint(r0.z) >> 1;
	r0.z = asfloat(-asint(r0.x));
	r0.w = asfloat(i.texcoord1.w & -2147483648);
	r0.x = (asint(r0.w) != 0) ? r0.z : r0.x;
	r0.x = (float)asint(r0.x);
	r0.z = asfloat((uint)i.texcoord.x);
	r0.z = asfloat((uint)asint(r0.z) >> 1);
	r0.z = (float)asint(r0.z);
	r0.x = -(r0.z) + r0.x;
	r0.z = asfloat((i.texcoord1.y != i.texcoord1.x) ? -1 : 0);
	r0.z = (asint(r0.z) != 0) ? i.texcoord.x : i.texcoord.y;
	o.w = r0.x + r0.z;
	r0.x = i.texcoord.x * 3.5;
	r0.x = asfloat((int)r0.x);
	r0.x = asfloat(asint(r0.x) + i.texcoord1.y);
	o.x = (float)asint(r0.x);
	r0.x = asfloat(-(int)r0.y);
	r0.z = asfloat(i.texcoord1.x ^ 2);
	r0.z = asfloat(asint(r0.z) & -2147483648);
	r0.x = (asint(r0.z) != 0) ? r0.x : r0.y;
	o.y = (float)asint(r0.x);

	return o;
}
