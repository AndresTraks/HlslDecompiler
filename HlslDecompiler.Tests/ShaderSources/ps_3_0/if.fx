sampler2D sampler0;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0;
	if (texcoord.y > 0) {
		t0 = tex2Dlod(sampler0, texcoord);
	} else {
		t0 = float4(1, 0, 3, 4);
	}
	if (texcoord.x <= 0) {
		return t0 + tex2D(sampler0, texcoord.xy);
	} else {
		return t0 + float4(1, 0, 3, 4);
	}
}
