sampler2D sampler0;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = tex2D(sampler0, texcoord.xy);
	float4 t1;
	if (texcoord.x > 0) {
		float4 t2 = tex2D(sampler0, texcoord.zw);
		t1 = t0 + t2;
	} else {
		float4 t3 = tex2D(sampler0, texcoord.wz);
		t1 = t0 - t3;
	}
	if (texcoord.y > 0) {
		float4 t4 = tex2D(sampler0, texcoord.yx);
		return t4 * t1;
	} else {
		float4 t5 = tex2D(sampler0, texcoord.xz);
		return t5 + t1;
	}
}
