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
	int4 r0;
	r0.x = s & 1;
	r0.yz = int2(2147483647, 33554431) << r0.xx;
	r0.xw = (int2(0, 0) & ~(int2(2147483647, 33554431) << r0.xx)) | ((b << r0.xx) & (int2(2147483647, 33554431) << r0.xx));
	r0.yz = ~r0.yz;
	r0.yz = r0.yz & v;
	r0.xy = r0.xw | r0.yz;
	o[0] = r0.x;
	o[1] = r0.y;
}
