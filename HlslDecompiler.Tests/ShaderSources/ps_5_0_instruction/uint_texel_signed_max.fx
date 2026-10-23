cbuffer C : register(b0)
{
	int bias;
};

Texture2D<uint2> t;

int4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	int4 o;

	int4 r0;
	int4 r1;
	int4 r2;
	r0.xy = (int2)sv_position.xy;
	r0.zw = int2(0, 0);
	r0.xy = t.Load(r0.xyz).xy;
	r0 = r0.xyxy + -(bias);
	r1.xy = r0.zw ^ int2(2, 2);
	r1.xy = r1.xy & int2(-2147483648, -2147483648);
	r2 = max(r0, -(r0.zwzw));
	r0.x = -(r0.w) + r0.z;
	r0.yz = (uint2)r2.xy >> uint2(1, 1);
	r0.w = (r2.w < r2.z) ? -1 : 0;
	o.z = r0.w & 1;
	r1.zw = -r0.yz;
	o.xy = (r1.xy != 0) ? r1.zw : r0.yz;
	r0.y = (0 < r0.x) ? -1 : 0;
	r0.x = (r0.x < 0) ? -1 : 0;
	o.w = -(r0.y) + r0.x;

	return o;
}
