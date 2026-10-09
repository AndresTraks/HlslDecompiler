float3 w;

SamplerState s;
Texture2D layers[3];

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = layers[1].Sample(s, texcoord.xy);
	r0 = r0 * w.y;
	r1 = layers[0].Sample(s, texcoord.xy);
	r0 = r1 * w.x + r0;
	r1.xy = texcoord.xy * float2(4, 4);
	r1 = layers[2].Sample(s, r1.xy);
	o = r1 * w.z + r0;

	return o;
}
