Texture2D tx;

float4 main() : SV_Target
{
	return tx.Load(float3(1, 2, 0));
}
