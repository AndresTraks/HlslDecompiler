cbuffer C : register(b0)
{
	int mode;
	int count;
};

float4 main() : SV_Target
{
	float4 o;

	int r0;
	int r1;
	r0 = (uint)mode % 3;
	o.x = (float)(uint)r0.x;
	r0 = (uint)mode / 5;
	o.y = (float)(uint)r0.x;
	r0 = mode | 1;
	uint divisor0 = (uint)r0.x;
	r0 = (uint)count / divisor0;
	r1 = (uint)count % divisor0;
	o.z = (float)(uint)r1.x;
	o.w = (float)(uint)r0.x;

	return o;
}
