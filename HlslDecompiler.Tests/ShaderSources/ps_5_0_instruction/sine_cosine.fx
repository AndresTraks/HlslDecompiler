float4 v;

float4 main() : SV_Target
{
	float4 o;

	float r0;
	float r1;
	r0 = v.x * 3;
	float angle0 = r0.x;
	r0 = sin(angle0);
	r1 = cos(angle0);
	o.x = r0.x;
	o.y = r1.x;
	o.zw = float2(0, 0);

	return o;
}
