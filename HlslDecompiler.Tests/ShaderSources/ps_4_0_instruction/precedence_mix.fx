struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation int4 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float3 r1;
	r0.x = asfloat(i.texcoord1.y & i.texcoord1.x);
	r0.x = asfloat(asint(r0.x) | i.texcoord1.z);
	r0.y = asfloat(i.texcoord1.w << 2);
	r0.x = asfloat(asint(r0.y) ^ asint(r0.x));
	r0.y = asfloat(~i.texcoord1.x);
	r0.y = asfloat(asint(r0.y) & i.texcoord1.y);
	r0.x = asfloat(asint(r0.y) + asint(r0.x));
	o.z = (float)asint(r0.x);
	r0.x = asfloat(i.texcoord1.y + i.texcoord1.x);
	r0.x = asfloat(asint(r0.x) * i.texcoord1.z);
	r0.y = asfloat(asint(r0.x) & -2147483648);
	r0.x = asfloat(max(asint(r0.x), -(asint(r0.x))));
	r0.x = asfloat(asuint(r0.x) - asuint(r0.x) / 5 * 5);
	r0.z = asfloat(-asint(r0.x));
	r0.x = (asint(r0.y) != 0) ? r0.z : r0.x;
	r0.x = (float)asint(r0.x);
	r0.y = saturate(asfloat(i.texcoord.w));
	r1 = asfloat((i.texcoord.xwy < i.texcoord.yzx) ? -1 : 0);
	r0.y = (asint(r1.z) != 0) ? -(abs(i.texcoord.z)) : r0.y;
	r0.z = asfloat(asint(r1.y) & asint(r1.x));
	r0.z = (asint(r0.z) != 0) ? i.texcoord.x : i.texcoord.y;
	o.w = r0.y + r0.x;
	r0.xy = asfloat((float2(0, 0) < i.texcoord.xy) ? -1 : 0);
	r0.y = (asint(r0.y) != 0) ? 1 : 2;
	r0.x = (asint(r0.x) != 0) ? r0.y : 3;
	r0.y = i.texcoord.y + i.texcoord.x;
	r1.xy = -(i.texcoord.wy) + i.texcoord.zx;
	r0.w = r0.y * r1.x;
	o.x = -(r0.y) * i.texcoord.z + r0.z;
	r0.y = r0.w / r1.y;
	o.y = r0.y * r0.x;

	return o;
}
