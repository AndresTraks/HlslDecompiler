float lod;
float bias;

SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return tex.SampleBias(samp, texcoord.xy, bias) * tex.SampleGrad(samp, texcoord.xy, texcoord.zw, texcoord.wz) + tex.SampleLevel(samp, texcoord.xy, lod) - tex.Load(float3((int2)(256 * texcoord.xy), 0));
}
