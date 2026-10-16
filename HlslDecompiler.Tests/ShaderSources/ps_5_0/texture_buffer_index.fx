cbuffer P : register(b1)
{
	uint which;
};

tbuffer Table : register(t0)
{
	float4 rows[4];
};

Buffer<uint> ids : register(t1);

float4 main() : SV_Target
{
	return rows[ids.Load(which).x & 3];
}
