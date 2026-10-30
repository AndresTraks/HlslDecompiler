cbuffer c : register(b0)
{
	int v;
	int b;
	uint s;
};

RWStructuredBuffer<int> o : register(u0);

[numthreads(1, 1, 1)]
void main()
{
	int2 t0 = int2(~(33554431 << (s & 1)) & v, ((b & 33554431) << (s & 1)));
	o[0] = ((b & 2147483647) << (s & 1)) | (~(2147483647 << (s & 1)) & v);
	o[1] = t0.x | t0.y;
}
