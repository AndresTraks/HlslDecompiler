float lod;
float bias;

SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	r0.xy = texcoord.xy * float2(256, 256);
	r0.xy = (int2)r0.xy;
	r0.zw = float2(0, 0);
	r0 = tex.Load(r0.xyz);
	r1 = tex.SampleLevel(samp, texcoord.xy, lod);
	r2 = tex.SampleBias(samp, texcoord.xy, bias);
	r3 = tex.SampleGrad(samp, texcoord.xy, texcoord.zwzz, texcoord.wzww);
	r1 = r2 * r3 + r1;
	o = -(r0) + r1;

	return o;
}
