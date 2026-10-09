float3 w;

SamplerState s;
Texture2D layers[3];

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = layers[0].Sample(s, texcoord);
	float4 t1 = layers[1].Sample(s, texcoord);
	float4 t2 = layers[2].Sample(s, 4 * texcoord);
	return t0 * w.x + t1 * w.y + t2 * w.z;
}
