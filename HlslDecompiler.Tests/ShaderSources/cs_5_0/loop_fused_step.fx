int n;

RWStructuredBuffer<float4> buf : register(u0);

[numthreads(8, 1, 1)]
void main()
{
	for (int t0 = 0; t0 < n; t0 = t0 + 1) {
		buf[t0] = 2 * buf[t0 + 3 & 7];
	}
}
