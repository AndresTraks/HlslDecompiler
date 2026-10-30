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
	o[0] = int4(u > 2147483647, i > -1, f < 0.5, u >= 7);
}
