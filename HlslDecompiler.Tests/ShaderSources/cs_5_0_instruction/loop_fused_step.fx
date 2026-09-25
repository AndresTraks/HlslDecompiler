int n;

RWStructuredBuffer<float4> buf : register(u0);

[numthreads(8, 1, 1)]
void main()
{
	int3 r0;
	float4 r1;
	r0.x = 0;
	while (true) {
		r0.y = (r0.x >= n) ? -1 : 0;
		if (r0.y != 0) break;
		r0.yz = r0.xx + int2(3, 1);
		r0.y = r0.y & 7;
		r1 = buf[r0.y];
		r1 = r1 + r1;
		buf[r0.x] = r1;
		r0.x = r0.z;
	}
}
