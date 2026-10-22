struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation int4 k : K;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	float4 x0[12];
	x0[0].x = 0;
	x0[1].x = i.texcoord.x;
	r0 = asint(i.texcoord + i.texcoord);
	x0[2].x = asfloat(r0.x);
	x0[3].x = 0;
	x0[4].x = i.texcoord.y;
	x0[5].x = asfloat(r0.y);
	x0[6].x = 0;
	x0[7].x = i.texcoord.z;
	x0[8].x = asfloat(r0.z);
	x0[9].x = 0;
	x0[10].x = i.texcoord.w;
	x0[11].x = asfloat(r0.w);
	r0.xy = max(i.k.yw, -(i.k.yw));
	r0.xy = (uint2)r0.xy % int2(3, 3);
	r0.zw = -r0.xy;
	r1 = i.k.yxzw & int4(-2147483648, 3, 3, -2147483648);
	r0.xy = (r1.xw != 0) ? r0.zw : r0.xy;
	r0.x = r1.y * 3 + r0.x;
	r0.z = r1.z * 3;
	x0[r0.x].x = 9;
	o.y = x0[r0.y + 3].x;
	o.x = x0[r0.z + 1].x;
	o.z = x0[11].x;
	o.w = x0[0].x;

	return o;
}
