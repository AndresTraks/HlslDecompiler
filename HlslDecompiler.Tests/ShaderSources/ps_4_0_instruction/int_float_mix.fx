struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation int4 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	int3 r1;
	r0.x = i.texcoord1.x << 1;
	r0.y = (i.texcoord1.y < 0) ? -1 : 0;
	r1 = -i.texcoord1.xyw;
	r0.y = (r0.y != 0) ? r1.y : i.texcoord1.y;
	r0.zw = max(r1.xz, i.texcoord1.xw);
	r0.x = r0.y + r0.x;
	r0.x = asint((float)r0.x);
	r0.y = asint(frac(i.texcoord.x));
	r1.x = asint((float)i.texcoord1.z);
	o.z = asfloat(r0.y) * asfloat(r1.x) + asfloat(r0.x);
	r0.x = (uint)r0.w % 3;
	r0.y = (uint)r0.z >> 1;
	r0.z = -r0.x;
	r0.w = i.texcoord1.w & -2147483648;
	r0.x = (r0.w != 0) ? r0.z : r0.x;
	r0.x = asint((float)r0.x);
	r0.z = (uint)i.texcoord.x;
	r0.z = (uint)r0.z >> 1;
	r0.z = asint((float)(uint)r0.z);
	r0.x = asint(-(asfloat(r0.z)) + asfloat(r0.x));
	r0.z = (i.texcoord1.y != i.texcoord1.x) ? -1 : 0;
	r0.z = asint((r0.z != 0) ? i.texcoord.x : i.texcoord.y);
	o.w = asfloat(r0.x) + asfloat(r0.z);
	r0.x = asint(i.texcoord.x * 3.5);
	r0.x = (int)asfloat(r0.x);
	r0.x = r0.x + i.texcoord1.y;
	o.x = (float)r0.x;
	r0.x = -r0.y;
	r0.z = i.texcoord1.x ^ 2;
	r0.z = r0.z & -2147483648;
	r0.x = (r0.z != 0) ? r0.x : r0.y;
	o.y = (float)r0.x;

	return o;
}
