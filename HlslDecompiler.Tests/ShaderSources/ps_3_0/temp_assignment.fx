sampler2D sampler0;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0;
	float2 t1 = texcoord.yz + 5;
	if (texcoord.y > 0) {
		t0 = tex2Dlod(sampler0, texcoord);
		t1 = t0.xy + t1;
	} else {
		t0 = tex2Dlod(sampler0, 2 * texcoord);
		t1 = t1 - t0.xy;
	}
	if (texcoord.y >= 0) {
		t1 = t1 + tex2Dlod(sampler0, texcoord + 1).xy;
	} else {
		t0 = float4(1, 0, 3, 4);
	}
	if (texcoord.x <= 0) {
		t1 = t1 + t0.xy;
		return t0 + tex2D(sampler0, t1.xy);
	} else {
		return float4(t0.x + 1, t0.y, t0.zw + float2(3, 4));
	}
}
