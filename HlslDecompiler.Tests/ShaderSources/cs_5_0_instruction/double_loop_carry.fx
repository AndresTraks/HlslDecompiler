cbuffer C : register(b0)
{
	double scale;
	uint n;
};

StructuredBuffer<double> a : register(t0);
RWStructuredBuffer<double> o : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	double d0;
	double2 d1;
	d0 = 0;
	r0.z = 0;
	while (true) {
		r0.w = ((uint)r0.z >= n) ? -1 : 0;
		if (r0.w != 0) break;
		d1.x = d0 * scale;
		d1.y = a[r0.z];
		d0 = d1.y + d1.x;
		r0.z = r0.z + 1;
	}
	o[sv_dispatchthreadid.x] = d0;
}
