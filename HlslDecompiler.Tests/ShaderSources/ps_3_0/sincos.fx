float4 k;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4(sin(6.28318548 * frac(0.159154937 * k.x * texcoord.x + 0.5) - 3.14159274) * k.x, cos(6.28318548 * frac(0.159154937 * k.x * texcoord.x + 0.5) - 3.14159274) * k.y, sin(6.28318548 * frac(0.159154937 * texcoord.y + 0.5) - 3.14159274) * k.z, cos(6.28318548 * frac(0.159154937 * texcoord.z + 0.5) - 3.14159274) * k.w);
}
