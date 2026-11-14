min16float4 c;

float4 main(min16float4 color : COLOR) : SV_Target
{
	float4 o;

	float4 r0;
	int r1;
	r0 = color * c;
	r1 = (r0.y < r0.x) ? -1 : 0;
	r0 = (r1.x != 0) ? r0 : r0.yzwx;
	o = r0;

	return o;
}
