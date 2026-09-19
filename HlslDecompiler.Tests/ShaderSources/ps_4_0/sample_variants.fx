float lod;
float bias;

SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = tex.SampleLevel(samp, texcoord.xy, lod);
	float4 t1 = tex.SampleGrad(samp, texcoord.xy, texcoord.zw, texcoord.wz);
	float4 t2 = tex.SampleBias(samp, texcoord.xy, bias);
	return t2 * t1 + t0 - tex.Load(int3((int2)(256 * texcoord.xy), 0));
}
