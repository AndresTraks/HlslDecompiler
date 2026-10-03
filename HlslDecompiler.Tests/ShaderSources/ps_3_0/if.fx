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
		float4 t1 = tex2D(sampler0, texcoord.xy);
		return t0 + t1;
	} else {
		return t0 + float4(1, 0, 3, 4);
	}
}
