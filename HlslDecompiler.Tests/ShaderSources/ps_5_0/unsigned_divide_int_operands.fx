cbuffer C : register(b0)
{
	int mode;
	int count;
};

float4 main() : SV_Target
{
	return float4((float)((uint)mode % 3), (float)((uint)mode / 5), (float)((uint)count % (mode | 1)), (float)((uint)count / (mode | 1)));
}
