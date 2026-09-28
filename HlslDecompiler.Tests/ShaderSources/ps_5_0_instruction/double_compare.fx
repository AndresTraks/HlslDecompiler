cbuffer K : register(b0)
{
	double k;
};

float4 main(noperspective float4 sv_position : SV_Position) : SV_TARGET
{
	float4 o;

	float4 r0;
	double2 d0;
	float4 r1;
	double2 d1;
	d0 = (double2)sv_position.xy;
	d0.y = d0.y * k;
	r1.x = (d0.x != d0.y) ? -1 : 0;
	if (asint(r1.x) != 0) {
		o = float4(1, 0, 0, 1);
		return o;
	}
	r1.x = (d0.x >= d0.y) ? -1 : 0;
	if (asint(r1.x) != 0) {
		o = float4(0, 1, 0, 1);
		return o;
	}
	r1.x = (d0.x < d0.y) ? -1 : 0;
	d0.x = d0.x + 1;
	d1.y = d0.y * 2;
	d0.x = (asint(r1.x) != 0) ? d0.x : d1.y;
	r0.z = (uint)d0.y;
	d0.y = (double)r0.z;
	d0.x = d0.y + d0.x;
	r0.x = (float)d0.x;
	o.x = r0.x;
	o.yzw = float3(0, 0, 1);

	return o;
}
