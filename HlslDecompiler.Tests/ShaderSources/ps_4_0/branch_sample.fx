bool useDetail;
float2 fade;

SamplerState samp;
Texture2D albedo;
Texture2D detail;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 t0;
	if (useDetail) {
		t0 = albedo.Sample(samp, i.texcoord) * detail.Sample(samp, 8 * i.texcoord);
	} else {
		t0 = albedo.Sample(samp, i.texcoord);
	}
	return lerp(t0, float4(0.5, 0.5, 0.5, 1), smoothstep(fade.x, fade.y, i.texcoord1));
}
