cbuffer C : register(b0)
{
	int mode;
	float4 arr[8];
};

float4 main() : SV_Target
{
	return arr[(mode >> 2) + 1] + arr[(mode & 3) + 2];
}
