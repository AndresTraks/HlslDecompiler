cbuffer C : register(b0)
{
	int mode;
	float4 arr[8];
};

float4 main() : SV_Target
{
	float4 o;

	int2 r0;
	r0.x = mode >> 2;
	r0.x = r0.x + 1;
	r0.y = mode & 3;
	o = arr[r0.x] + arr[r0.y + 2];

	return o;
}
