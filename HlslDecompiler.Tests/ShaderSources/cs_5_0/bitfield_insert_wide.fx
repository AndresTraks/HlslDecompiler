cbuffer c : register(b0)
{
	int a;
	int b;
};

RWStructuredBuffer<int> o : register(u0);

[numthreads(1, 1, 1)]
void main()
{
	o[0] = (1 & 1) | ((a << 1) & -2);
	o[1] = (a & 255) | ((b << 8) & -256);
}
