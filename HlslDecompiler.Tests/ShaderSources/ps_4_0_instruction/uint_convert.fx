float4 v;

float4 main() : SV_Target
{
	float4 o;

	int2 r0;
	o.w = trunc(v.y);
	r0.x = v.y;
	r0.y = v.x;
	r0.x = r0.x + r0.y;
	o.xz = r0.xy;
	r0.x = r0.y << 1;
	o.y = r0.x;

	return o;
}
