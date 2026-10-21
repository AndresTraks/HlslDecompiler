struct PS_IN
{
	nointerpolation int4 a : A;
	float4 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	int4 r2;
	r0.xy = i.a.zw | i.a.xy;
	r0.x = r0.y | r0.x;
	r0.x = (r0.x != 0) ? -1 : 0;
	r0.y = (i.texcoord.y < i.texcoord.x) ? -1 : 0;
	r0.z = (i.texcoord.z < 0) ? -1 : 0;
	r0.y = r0.z & r0.y;
	r0.y = r0.y | r0.x;
	r1 = (i.a.yzwx == i.a) ? -1 : 0;
	r1.xy = r1.zw & r1.xy;
	r0.z = r1.y & r1.x;
	r1 = (int4(1, 1, 1, 1) < i.a) ? -1 : 0;
	r2.x = r1.w & r1.z;
	r2.x = ~r2.x;
	r2.y = r1.y | r1.x;
	r0.x = r2.x & r2.y;
	r2 = (float4(0, 0, 0, 0) < i.texcoord) ? -1 : 0;
	r1 = (r1 != r2) ? -1 : 0;
	r1.xy = r1.zw | r1.xy;
	r0.w = r1.y | r1.x;
	o = asfloat(r0 & int4(1065353216, 1065353216, 1065353216, 1065353216));

	return o;
}
