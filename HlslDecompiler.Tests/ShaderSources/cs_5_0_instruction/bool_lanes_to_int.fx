cbuffer c : register(b0)
{
	uint u;
	int i;
	float f;
};

RWStructuredBuffer<uint4> o : register(u0);

[numthreads(1, 1, 1)]
void main()
{
	int4 r0;
	r0.x = (2147483647 < u) ? -1 : 0;
	r0.y = (-1 < i) ? -1 : 0;
	r0.z = (f < 0.5) ? -1 : 0;
	r0.w = (u >= 7) ? -1 : 0;
	r0 = r0 & int4(1, 1, 1, 1);
	o[0] = r0;
}
