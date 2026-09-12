float4 v;

float4 main() : SV_Target
{
	float4 o;

	int2 r0;
	o.w = trunc(v.y);
	r0.x = (int)v.y;
	r0.y = (uint)v.x;
	r0.x = r0.x + r0.y;
	o.xz = (float2)(uint2)r0.xy;
	r0.x = r0.y << 1;
	o.y = (float)(uint)r0.x;

	return o;
}
