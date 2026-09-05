Texture2D tx;

float4 main() : SV_Target
{
	float4 o;

	o = tx.Load(1);

	return o;
}
