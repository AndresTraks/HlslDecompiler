cbuffer P : register(b1)
{
	uint which;
};

tbuffer Table
{
	float4 rows[4];
};

Buffer<uint> ids : register(t1);

float4 main() : SV_Target
{
	float4 o;

	int r0;
	r0 = ids.Load(which).x;
	r0 = r0.x & 3;
	o = rows[r0.x];

	return o;
}
