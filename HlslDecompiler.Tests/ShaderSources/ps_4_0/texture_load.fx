Texture2D tx;

float4 main() : SV_Target
{
	return tx.Load(int3(1, 2, 0));
}
