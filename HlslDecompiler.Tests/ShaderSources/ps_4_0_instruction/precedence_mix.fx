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
	r0.x = i.texcoord1.y & i.texcoord1.x;
	r0.x = r0.x | i.texcoord1.z;
	r0.y = i.texcoord1.w << 2;
	r0.x = r0.y ^ r0.x;
	r0.y = ~i.texcoord1.x;
	r0.y = r0.y & i.texcoord1.y;
	r0.x = r0.y + r0.x;
	o.z = (float)r0.x;
	r0.x = i.texcoord1.y + i.texcoord1.x;
	r0.x = r0.x * i.texcoord1.z;
	r0.y = r0.x & -2147483648;
	r0.x = max(r0.x, -(r0.x));
	r0.x = (uint)r0.x % 5;
	r0.z = -r0.x;
	r0.x = (r0.y != 0) ? r0.z : r0.x;
	r0.x = asint((float)r0.x);
	r0.y = asint(saturate(i.texcoord.w));
	r1 = (i.texcoord.xwy < i.texcoord.yzx) ? -1 : 0;
	r0.y = (r1.z != 0) ? asint(-(abs(i.texcoord.z))) : r0.y;
	r0.z = r1.y & r1.x;
	r0.z = asint((r0.z != 0) ? i.texcoord.x : i.texcoord.y);
	o.w = asfloat(r0.y) + asfloat(r0.x);
	r0.xy = (float2(0, 0) < i.texcoord.xy) ? -1 : 0;
	r0.y = (r0.y != 0) ? 1065353216 : 1073741824;
	r0.x = (r0.x != 0) ? r0.y : 1077936128;
	r0.y = asint(i.texcoord.y + i.texcoord.x);
	r1.xy = asint(-(i.texcoord.wy) + i.texcoord.zx);
	r0.w = asint(asfloat(r0.y) * asfloat(r1.x));
	o.x = -(asfloat(r0.y)) * i.texcoord.z + asfloat(r0.z);
	r0.y = asint(asfloat(r0.w) / asfloat(r1.y));
	o.y = asfloat(r0.y) * asfloat(r0.x);

	return o;
}
