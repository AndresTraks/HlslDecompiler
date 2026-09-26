cbuffer Params : register(b0)
{
	uint offset;
};

StructuredBuffer<float> input : register(t0);
RWStructuredBuffer<float> output : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	float4 x0[8];
	r0.x = asint(input[sv_dispatchthreadid.x]);
	x0[0].x = asfloat(r0.x);
	r0 = sv_dispatchthreadid.x + int4(1, 2, 3, 4);
	r0.x = asint(input[r0.x]);
	x0[1].x = asfloat(r0.x);
	r0.x = asint(input[r0.y]);
	x0[2].x = asfloat(r0.x);
	r0.x = asint(input[r0.z]);
	r0.y = asint(input[r0.w]);
	x0[3].x = asfloat(r0.x);
	x0[4].x = asfloat(r0.y);
	r0.xyz = sv_dispatchthreadid.xxx + int3(5, 6, 7);
	r0.x = asint(input[r0.x]);
	x0[5].x = asfloat(r0.x);
	r0.x = asint(input[r0.y]);
	r0.y = asint(input[r0.z]);
	x0[6].x = asfloat(r0.x);
	x0[7].x = asfloat(r0.y);
	r0.x = offset & 5;
	r0.y = r0.x + 2;
	r0.y = asint(x0[r0.y].x);
	r0.z = asint(x0[r0.x + 1].x);
	r0.x = asint(x0[r0.x].x);
	r0.x = asint(asfloat(r0.x) * 0.5);
	r0.y = asint(asfloat(r0.y) + asfloat(r0.z));
	r0.x = asint(asfloat(r0.y) * 0.25 + asfloat(r0.x));
	output[sv_dispatchthreadid.x] = asfloat(r0.x);
}
