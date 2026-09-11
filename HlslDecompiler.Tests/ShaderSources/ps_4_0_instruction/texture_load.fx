Texture2D tx;

float4 main() : SV_Target
{
	float4 o;

	o = tx.Load(int3(1, 2, 0));

	return o;
}
