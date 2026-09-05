sampler2D sampler0;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float2 t0 = 5 + texcoord.yz;
	float4 t1;
	float2 t2;
	if (texcoord.y > 0) {
		t1 = tex2Dlod(sampler0, texcoord);
		t2 = t0 + t1.xy;
	} else {
		t1 = tex2Dlod(sampler0, 2 * texcoord);
		t2 = t0 - t1.xy;
	}
	if (texcoord.y >= 0) {
		t2 = t2 + tex2Dlod(sampler0, 1 + texcoord).xy;
	} else {
		t1 = float4(1, 0, 3, 4);
	}
	return float4(-texcoord.x >= 0 ? tex2D(sampler0, t2 + t1.xy).x + t1.x : t1.x + 1, -texcoord.x >= 0 ? tex2D(sampler0, t2 + t1.xy).y + t1.y : t1.y, -texcoord.x >= 0 ? tex2D(sampler0, t2 + t1.xy).zw + t1.zw : t1.zw + float2(3, 4));
}
