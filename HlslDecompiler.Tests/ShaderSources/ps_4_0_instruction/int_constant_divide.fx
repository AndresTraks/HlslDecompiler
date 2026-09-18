int value;
uint uvalue;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	float4 r1;
	r0.x = value ^ 7;
	r0.x = r0.x & -2147483648;
	r0.y = max(value, -(value));
	r0.z = (uint)r0.y / 7;
	r0.y = (uint)r0.y % 13;
	r0.w = -r0.z;
	r0.x = (r0.x != 0) ? r0.w : r0.z;
	r1.x = (float)r0.x;
	r0.x = -r0.y;
	r0.z = value & -2147483648;
	r0.x = (r0.z != 0) ? r0.x : r0.y;
	r1.y = (float)r0.x;
	r0.x = (uint)uvalue / 10;
	r1.z = (float)(uint)r0.x;
	r0.x = (uint)uvalue % 6;
	r1.w = (float)(uint)r0.x;
	o = r1 * texcoord;

	return o;
}
