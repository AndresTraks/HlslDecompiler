int a;
uint b;
int4 v;

float4 main() : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	r0.xy = v.xw ^ int2(3, 3);
	r0.xy = r0.xy & int2(-2147483648, -2147483648);
	r0.zw = max(v.xw, -(v.xw));
	r1.xy = r0.zw / int2(3, 3);
	r0.zw = r0.zw % int2(7, 7);
	r1.zw = -r1.xy;
	r0.xy = (r0.xy != 0) ? r1.zw : r1.xy;
	r1.xy = -r0.zw;
	r1.zw = v.xw & int2(-2147483648, -2147483648);
	r0.zw = (r1.zw != 0) ? r1.xy : r0.zw;
	r0.xy = r0.xy + r0.zw;
	o.zw = (float2)(int2)r0.xy;
	r0.x = a << 3;
	r0.y = a >> 2;
	r0.x = r0.y | r0.x;
	o.x = (float)(int)r0.x;
	r0.x = (uint)b >> 1;
	r0.x = r0.x & 15;
	o.y = (float)(uint)r0.x;

	return o;
}
