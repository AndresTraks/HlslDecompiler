cbuffer cb : register(b0)
{
	float4x4 m[8];
	int idx;
};

float4 main(float4 position : POSITION) : SV_Position
{
	float4 o;

	int2 r0;
	float4 r1;
	r0.x = idx + 1;
	r0.x = (0 & ~(((1 << 3) - 1) << 2)) | ((r0.x << 2) & (((1 << 3) - 1) << 2));
	r0.y = idx << 2;
	r1.x = dot(position, transpose(m[r0.y / 4])[0]);
	r1.y = dot(position, transpose(m[r0.y / 4])[1]);
	r1.z = dot(position, transpose(m[r0.y / 4])[2]);
	r1.w = dot(position, transpose(m[r0.y / 4])[3]);
	o.x = dot(r1, transpose(m[r0.x / 4])[0]);
	o.y = dot(r1, transpose(m[r0.x / 4])[1]);
	o.z = dot(r1, transpose(m[r0.x / 4])[2]);
	o.w = dot(r1, transpose(m[r0.x / 4])[3]);

	return o;
}
