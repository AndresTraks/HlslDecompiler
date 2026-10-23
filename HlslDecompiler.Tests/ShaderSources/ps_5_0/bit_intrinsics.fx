uint mask;
int sv;

float4 main(nointerpolation uint4 a : A) : SV_Target
{
	return (float)firstbithigh(sv) + float4((float)countbits(a.x & mask), (float)((uint)reversebits(a.y) >> 28), (float)firstbithigh(a.z), (float)firstbitlow(a.w | 256));
}
