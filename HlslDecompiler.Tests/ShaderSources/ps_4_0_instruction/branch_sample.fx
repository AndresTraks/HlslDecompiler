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
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	if (useDetail != 0) {
		r0 = albedo.Sample(samp, i.texcoord.xy);
		r1.xy = i.texcoord.xy * float2(8, 8);
		r1 = detail.Sample(samp, r1.xy);
		r0 = r0 * r1;
	} else {
		r0 = albedo.Sample(samp, i.texcoord.xy);
	}
	r1.x = -(fade.x) + fade.y;
	r1.y = i.texcoord1 + -(fade.x);
	r1.x = float1(1) / r1.x;
	r1.x = saturate(r1.x * r1.y);
	r1.y = r1.x * -2 + 3;
	r1.x = r1.x * r1.x;
	r1.x = r1.x * r1.y;
	r2 = -(r0) + float4(0.5, 0.5, 0.5, 1);
	o = r1.x * r2 + r0;

	return o;
}
