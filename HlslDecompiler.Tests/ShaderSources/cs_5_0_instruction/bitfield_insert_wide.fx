cbuffer c : register(b0)
{
	int a;
	int b;
};

RWStructuredBuffer<int> o : register(u0);

[numthreads(1, 1, 1)]
void main()
{
	int r0;
	r0 = (1 & ~(2147483647 << 1)) | ((a << 1) & (2147483647 << 1));
	o[0] = r0.x;
	r0 = (a & ~(16777215 << 8)) | ((b << 8) & (16777215 << 8));
	o[1] = r0.x;
}
