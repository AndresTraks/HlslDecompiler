cbuffer Params : register(b0)
{
	double scale;
	double bias;
	double2 range;
};

RWBuffer<float4> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	double2 d0;
	int3 r1;
	d0.x = (double)sv_dispatchthreadid.x;
	d0.x = d0.x * scale;
	d0.x = d0.x + bias;
	r0.z = asint((float)d0.x);
	output[sv_dispatchthreadid.x] = asfloat(r0.z);
	d0.y = max(d0.x, range.x);
	r0.x = asint((d0.x < range.x) ? -1 : 0);
	r0.x = r0.x & 1065353216;
	d0.y = min(d0.y, range.y);
	r0.y = asint((float)d0.y);
	r1 = sv_dispatchthreadid.xxx + int3(1, 2, 3);
	output[r1.x] = asfloat(r0.y);
	output[r1.y] = asfloat(r0.x);
	d0.x = scale / bias;
	r0.x = asint((float)d0.x);
	output[r1.z] = asfloat(r0.x);
}
