SamplerState samp;
Texture2D<unorm float4> a;
Texture2D<snorm float4> b;
Texture2D<int4> c;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return a.Sample(samp, texcoord.xy) + b.Sample(samp, texcoord.xy) + (float4)(c.Load(int3((int2)texcoord.xy, 0)));
}
