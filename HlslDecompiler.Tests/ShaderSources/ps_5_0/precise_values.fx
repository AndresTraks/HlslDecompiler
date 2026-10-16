float4 a;
float4 b;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	precise float4 t0 = texcoord * a + b;
	return t0 * texcoord.x;
}
